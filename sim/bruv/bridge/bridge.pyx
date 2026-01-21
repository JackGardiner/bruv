# man get me into c as quickly as possible.

# See ./readme.md for an explanation.

cdef extern from "bridge.h":
    ctypedef unsigned long long c_IH
    ctypedef unsigned int c_IHentry
    cdef c_IHentry C_F64
    cdef c_IHentry C_I64
    cdef c_IHentry C_PTR_F32
    cdef c_IHentry C_PTR_F64
    cdef c_IHentry C_PTR_I8
    cdef c_IHentry C_PTR_I16
    cdef c_IHentry C_PTR_I32
    cdef c_IHentry C_PTR_I64
    cdef c_IHentry C_PTR_U8
    cdef c_IHentry C_PTR_U16
    cdef c_IHentry C_PTR_U32
    cdef c_IHentry C_PTR_U64
    cdef c_IHentry C_INPUT
    cdef c_IHentry C_OUTPUT
    cdef c_IHentry C_INPUT_DATA
    cdef c_IHentry C_OUTPUT_DATA
    c_IH c_ih_initial()
    c_IH c_ih_add(c_IH running, const char* add_name, c_IHentry add_entry)

    ctypedef unsigned long long c_eight_bytes
    const char* c_execute(c_eight_bytes* state, c_IH interpretation_hash)


from libc.stdlib cimport malloc, free
from libc.string cimport memcpy
import numpy as np
cimport numpy as np
np.import_array()



FLAG_INPUT = C_INPUT
FLAG_OUTPUT = C_OUTPUT
FLAG_INPUT_DATA = C_INPUT_DATA
FLAG_OUTPUT_DATA = C_OUTPUT_DATA
_FLAG_ALL = FLAG_INPUT | FLAG_OUTPUT | FLAG_INPUT_DATA | FLAG_OUTPUT_DATA

cdef class Interpretation:
    def __init__(Interpretation self):
        self._running = c_ih_initial()
        self._length = 0
        self._finalised = 0

    _ALLOWED_KINDS = {
        "f64": C_F64,
        "i64": C_I64,
        "f32[]": C_PTR_F32,
        "f64[]": C_PTR_F64,
        "i8[]":  C_PTR_I8,
        "i16[]": C_PTR_I16,
        "i32[]": C_PTR_I32,
        "i64[]": C_PTR_I64,
        "u8[]":  C_PTR_U8,
        "u16[]": C_PTR_U16,
        "u32[]": C_PTR_U32,
        "u64[]": C_PTR_U64,
    }
    def add(self, str name, str kind, flags):
        if type(flags) is not int:
            raise ValueError("expected integer flags, got "
                            f"{repr(type(flags).__name__)}")
        if kind not in self._ALLOWED_KINDS:
            raise ValueError(f"invalid kind: {repr(kind)}")
        if (flags & ~_FLAG_ALL) != 0:
            raise ValueError(f"invalid flags: 0x{flags:X}")
        if self._finalised != 0:
            raise RuntimeError("cannot modify finalised interpretation")
        # Convert to the args c expects.
        cdef c_IHentry entry = <c_IHentry>self._ALLOWED_KINDS[kind]
        entry |= <c_IHentry>flags
        b_name = name.encode("utf-8")
        cdef const char* c_name = <const char*>b_name
        self._running = c_ih_add(self._running, c_name, entry)
        self._length += 1

    def finalise(self):
        self._finalised = 1


    # PRIVATE

    cdef c_IH _running
    cdef int _length
    cdef int _finalised



cdef class State:
    def __cinit__(State self, Interpretation interp):
        self._array = NULL # in-case of throw
        if not interp._finalised:
            raise ValueError("requires finalised interpretation")
        self._array = <c_eight_bytes*>malloc(interp._length * 8)
        if self._array == NULL:
            raise MemoryError("cooked")
        self._interp = interp


    # STATE GETTERS

    def get_f64(State self, int idx):
        cdef c_eight_bytes raw = self._get(idx)
        cdef double val
        memcpy(&val, &raw, 8)
        return val

    def get_i64(State self, int idx):
        cdef c_eight_bytes raw = self._get(idx)
        cdef long long val
        memcpy(&val, &raw, 8)
        return val

    def get_array(State self, int idx, int numel, object dtype):
        cdef object dt = self._checked_dtype(dtype)
        assert idx >= 0
        assert numel >= 0

        cdef c_eight_bytes raw = self._get_raw(idx)
        cdef void* ptr
        memcpy(&ptr, &raw, 8)
        if ptr == NULL:
            assert numel == 0
            return None
        cdef long long arrsize = numel * dt.itemsize
        cdef unsigned char[:] view = <unsigned char[:arrsize]>ptr
        return np.asarray(view, copy=False).view(dt)


    # STATE SETTERS

    def set_f64(State self, int idx, double val):
        cdef c_eight_bytes raw
        memcpy(&raw, &val, 8)
        self._set(idx, raw)

    def set_i64(State self, int idx, long long val):
        cdef c_eight_bytes raw
        memcpy(&raw, &val, 8)
        self._set(idx, raw)

    def set_array_null(State self, int idx):
        self._set(idx, <c_eight_bytes>0)
    def set_array(State self, int idx, object arr):
        if arr is None:
            self._set(idx, <c_eight_bytes>0)
            return

        if arr.ndim != 1:
            raise TypeError("array must be 1D")
        if not arr.flags["C_CONTIGUOUS"]:
            raise TypeError("array must be C-contiguous.")
        cdef object dt = self._checked_dtype(arr.dtype)

        cdef c_eight_bytes raw
        cdef void* val = np.PyArray_DATA(arr)
        memcpy(&raw, &val, 8)
        self._set(idx, raw)


    # EXECUTE

    def execute(State self):
        """
        Executes the sim library on the current state. Returns None on success,
        otherwise a string detailing the error that occurred (the first line of
        this error string will always be the source location).
        """
        # Since we already have our state array in a format c can work with, we
        # can jus hand it over.
        cdef const char* ret = c_execute(self._array, self._interp._running)
        # Return success or convert ret to string.
        if ret == NULL:
            return None
        return ret.decode("utf-8")



    # PRIVATE

    _ALLOWED_KINDS = {b"f": {4, 8}, b"i": {1, 2, 4, 8}, b"u": {1, 2, 4, 8}}
    def _checked_dtype(State self, object dtype):
        cdef object dt = np.dtype(dtype)
        if dt.kind not in self._ALLOWED_KINDS:
            raise TypeError("unsupported dtype")
        if dt.itemsize not in self._ALLOWED_KINDS[dt.kind]:
            raise TypeError("unsupported dtype")
        if dt.byteorder not in {"=", "|"}:
            raise TypeError("unsupported dtype")
        return dt

    cdef Interpretation _interp
    cdef c_eight_bytes* _array

    def _get(State self, int idx):
        if not (0 <= idx and idx < self._interp._length):
            raise IndexError(f"index out of bounds: {idx}")
        return self._array[idx]
    def _set(State self, int idx, c_eight_bytes value):
        if not (0 <= idx and idx < self._interp._length):
            raise IndexError(f"index out of bounds: {idx}")
        self._array[idx] = value

    def __dealloc__(self):
        # textbook pointer deallocation. right proper stuff.
        free(self._array)
        self._array = NULL
