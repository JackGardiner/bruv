# man get me into c as quickly as possible.

# Very simple access into a state array, able to interpret each 8B element as a
# float, an int, or a buffer. Note that the cython enforces no layout of the
# state array, and leaves that entirely to the caller/constructor.

cdef extern from "bridge.h":
    ctypedef unsigned long long c_eight_bytes
    const char* c_execute(c_eight_bytes* c_input)


from libc.string cimport memcpy
import numpy as np
cimport numpy as np
np.import_array()


cdef class Bridge:

    # STATE GETTERS

    def get_float(self, int idx):
        cdef c_eight_bytes raw = self._get(idx)
        cdef double val
        memcpy(&val, &raw, 8)
        return val

    def get_int(self, int idx):
        cdef c_eight_bytes raw = self._get(idx)
        cdef long long val
        memcpy(&val, &raw, 8)
        return val

    def get_array(self, int idx, int numel, object dtype):
        cdef object dt = self._checked_dtype(dtype)
        assert idx >= 0
        assert numel >= 0

        cdef c_eight_bytes raw = self._get_raw(idx)
        cdef void* ptr;
        memcpy(&ptr, &raw, 8)
        if ptr == NULL:
            assert numel == 0
            return None
        cdef long long arrsize = numel * dt.itemsize
        cdef unsigned char[:] view = <unsigned char[:arrsize]>ptr
        return np.asarray(view, copy=False).view(dt)


    # STATE SETTERS

    def set_float(self, int idx, double val):
        cdef c_eight_bytes raw
        memcpy(&raw, &val, 8)
        self._set(idx, raw)

    def set_int(self, int idx, long long val):
        cdef c_eight_bytes raw
        memcpy(&raw, &val, 8)
        self._set(idx, raw)

    def set_array_null(self, int idx):
        self._set(idx, <c_eight_bytes>0)
    def set_array(self, int idx, object arr):
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

    def execute(Bridge self):
        """
        Executes the sim library on the current state. Returns None on success,
        otherwise a string detailing the error that occurred (the first line of
        this error string will always be the source location).
        """
        # Create the contiguous 8B array.
        cdef Py_ssize_t N = len(self._entries)
        cdef np.ndarray[c_eight_bytes, ndim=1] buffer;
        buffer = np.empty(N, dtype=np.uint64)
        for i in range(N):
            buffer[i] = self._entries[i]
        cdef c_eight_bytes* c_input = <c_eight_bytes*>buffer.data
        # Send to c.
        cdef const char* ret;
        ret = c_execute(c_input)
        if ret == NULL:
            return None
        return ret.decode("UTF-8")



    # PRIVATE

    # Keep a buffer of the 8B entries. This is modified/queried by the python
    # front-end and passed to the c back-end.
    cdef list _entries
    cdef c_eight_bytes _get(self, int idx):
        return <c_eight_bytes>self._entries[idx]
    cdef void _set(self, int idx, c_eight_bytes value):
        if idx >= len(self._entries):
            self._entries.extend([0] * (idx + 1 - len(self._entries)))
        self._entries[idx] = value
    def __init__(self):
        self._entries = []

    _ALLOWED_KINDS = {b"f": {4, 8}, b"i": {1, 2, 4, 8}, b"u": {1, 2, 4, 8}}
    def _checked_dtype(self, dtype):
        cdef object dt = np.dtype(dtype)
        if dt.kind not in self._ALLOWED_KINDS:
            raise TypeError("unsupported dtype")
        if dt.itemsize not in self._ALLOWED_KINDS[dt.kind]:
            raise TypeError("unsupported dtype")
        if dt.byteorder not in {"=", "|"}:
            raise TypeError("unsupported dtype")
        return dt
