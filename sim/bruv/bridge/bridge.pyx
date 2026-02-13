# man get me into c as quickly as possible.

# See ./readme.txt for an explanation.

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
    c_IH c_ih_append(c_IH running, const char* name, c_IHentry entry)

    ctypedef unsigned long long c_eight_bytes
    const char* c_execute(c_eight_bytes* state, c_IH interpretation_hash)


from libc.stdlib cimport malloc, free
from libc.string cimport memcpy
import numpy as np
cimport numpy as np
np.import_array()


# NOTE: ./__init__.py is what the frontend actually interacts with when accessing
#       the cython. If additional symbols are intended to be exported, modify the
#       `__all__` in there.



cdef class Interpretation:

    F64 = C_F64
    I64 = C_I64
    PTR_F32 = C_PTR_F32
    PTR_F64 = C_PTR_F64
    PTR_I8  = C_PTR_I8
    PTR_I16 = C_PTR_I16
    PTR_I32 = C_PTR_I32
    PTR_I64 = C_PTR_I64
    PTR_U8  = C_PTR_U8
    PTR_U16 = C_PTR_U16
    PTR_U32 = C_PTR_U32
    PTR_U64 = C_PTR_U64
    TYPES = {
        C_F64, C_I64,
        C_PTR_F32, C_PTR_F64,
        C_PTR_I8, C_PTR_I16, C_PTR_I32, C_PTR_I64,
        C_PTR_U8, C_PTR_U16, C_PTR_U32, C_PTR_U64,
    }

    INPUT  = C_INPUT
    OUTPUT = C_OUTPUT
    INPUT_DATA  = C_INPUT_DATA
    OUTPUT_DATA = C_OUTPUT_DATA
    FLAGS = C_INPUT | C_OUTPUT | C_INPUT_DATA | C_OUTPUT_DATA

    def __init__(Interpretation self):
        self._hash = c_ih_initial()
        self._mapping = {}
        self._length = 0
        self._finalised = 0

    def append(Interpretation self, str name, object itype, object iflags):
        """
        Appends the given member to the state interpretation. `name` should be a
        string of the member name, `itype` should be a type (`F64`, `I64`,
        or `PTR_*`), and `iflags` should be a bitwise combination of any
        requirement flags (`INPUT`, `OUTPUT`, `INPUT_DATA`, and `OUTPUT_DATA`).
        """
        if type(itype) is not int:
            raise ValueError("expected integer type, got "
                            f"{repr(type(itype).__name__)}")
        if type(iflags) is not int:
            raise ValueError("expected integer flags, got "
                            f"{repr(type(itype).__name__)}")
        if itype not in self.TYPES:
            raise ValueError(f"unrecognised type: 0x{itype:X}")
        if (iflags & ~self.FLAGS) != 0:
            raise ValueError(f"unrecognised flags: 0x{itype:X}")
        if self._finalised != 0:
            raise RuntimeError("cannot modify finalised interpretation")
        if name in self._mapping:
            raise ValueError(f"name already exists: {repr(name)}")

        # Convert to the args c expects.
        bytesname = name.encode("utf-8") # also owns the memory.
        cdef const char* c_name = <const char*>bytesname
        cdef c_IHentry c_entry = <c_IHentry>itype | <c_IHentry>iflags
        self._hash = c_ih_append(self._hash, c_name, c_entry)
        self._mapping[name] = (int(self._length), itype, iflags)
        self._length += 1

    def finalise(Interpretation self):
        """
        Singals that the interpretation is finished and cannot be modified in the
        future.
        """
        self._finalised = 1


    # PRIVATE

    cdef c_IH _hash
    cdef dict _mapping
    cdef int _length
    cdef int _finalised




cdef class Buffer:
    cdef void* ptr
    cdef object dt

    def view(self, numel):
        if type(numel) is not int:
            raise ValueError("expected integer numel, got "
                            f"{repr(type(numel).__name__)}")
        if self.ptr == NULL:
            if numel != 0:
                raise MemoryError("expected non-empty buffer")
            return None
        cdef long long totalsize = <long long>(numel * self.dt.itemsize)
        cdef unsigned char[:] view = <unsigned char[:totalsize]>self.ptr
        return np.asarray(view, copy=False).view(self.dt)


cdef class State:
    def __cinit__(State self, Interpretation interp):
        """
        Creates an uninitialised state array, interpreted as per the given
        `interp`.
        """
        self._array = NULL # in-case of throw
        if not interp._finalised:
            raise ValueError("requires finalised interpretation")
        self._array = <c_eight_bytes*>malloc(interp._length * 8)
        if self._array == NULL:
            raise MemoryError("cooked")
        self._interp = interp


    def __getitem__(State self, str name):
        """
        Returns the slot `name` (typed according to the interpretation).
        """
        if name not in self._interp._mapping:
            raise KeyError(f"missing name: {repr(name)}")
        idx, itype, _ = self._interp._mapping[name]

        cdef c_eight_bytes raw = self._get(idx)
        cdef double asfloat
        cdef long long asint
        cdef void* ptr

        if itype == Interpretation.F64:
            memcpy(&asfloat, &raw, 8)
            return asfloat
        elif itype == Interpretation.I64:
            memcpy(&asint, &raw, 8)
            return asint
        else:
            # otherwise an array type.
            dt = self._TO_DTYPE[itype]
            memcpy(&ptr, &raw, 8)
            buffer = Buffer()
            buffer.ptr = ptr
            buffer.dt = dt
            return buffer


    def __setitem__(State self, str name, object value):
        """
        Sets the slot `name` to the given `value` (validating its type against
        the interpretation).
        """
        if name not in self._interp._mapping:
            raise KeyError(f"missing name: {repr(name)}")
        idx, itype, _ = self._interp._mapping[name]

        cdef c_eight_bytes raw
        cdef double asfloat
        cdef long long asint
        cdef void* ptr

        if itype == Interpretation.F64:
            asfloat = value
            memcpy(&raw, &asfloat, 8)
        elif itype == Interpretation.I64:
            asint = value
            memcpy(&raw, &asint, 8)
        else:
            # otherwise an array type.
            if value is None: # treat as null pointer.
                raw = <c_eight_bytes>0
            else:
                if value.ndim != 1:
                    raise TypeError("array must be 1D")
                if not value.flags["C_CONTIGUOUS"]:
                    raise TypeError("array must be C-contiguous")
                if not value.flags["ALIGNED"]:
                    raise TypeError("array must be aligned")
                dt = value.dtype
                edt = self._TO_DTYPE[itype]
                if dt != edt:
                    raise TypeError(f"incorrect array dtype, expected {edt}, "
                                    f"got {dt}")
                ptr = np.PyArray_DATA(value)
                memcpy(&raw, &ptr, 8)

        self._set(idx, raw)


    def execute(State self):
        """
        Executes the sim library on the current state. Returns None on success,
        otherwise a string detailing the error that occurred (the first line of
        this error string will always be the source location).
        """
        # Since we already have our state array in a format c can work with, we
        # can jus hand it over.
        cdef const char* ret = c_execute(self._array, self._interp._hash)
        # Return success or convert ret to string.
        if ret == NULL:
            return None
        return ret.decode("utf-8")



    # PRIVATE

    _TO_DTYPE = {                        # native endianness.
        Interpretation.PTR_F32: np.dtype("=f4"),
        Interpretation.PTR_F64: np.dtype("=f8"),
        Interpretation.PTR_I8:  np.dtype("i1"),
        Interpretation.PTR_I16: np.dtype("=i2"),
        Interpretation.PTR_I32: np.dtype("=i4"),
        Interpretation.PTR_I64: np.dtype("=i8"),
        Interpretation.PTR_U8:  np.dtype("u1"),
        Interpretation.PTR_U16: np.dtype("=u2"),
        Interpretation.PTR_U32: np.dtype("=u4"),
        Interpretation.PTR_U64: np.dtype("=u8"),
    }

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
