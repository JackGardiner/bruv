// c my beloved.

#ifndef BRIDGE_H_
#define BRIDGE_H_

// Only expose a state array and execute function. The python front-end is
// responsible for interpreting the state array in the correct way.


// bloody msvc.
#ifndef __GCC__
  #define __attribute(...) /* ignored */
#endif

typedef char assert_size_f64_[(sizeof(double) == 8) ? 1 : -1];
typedef char assert_size_i64_[(sizeof(long long) == 8) ? 1 : -1];
typedef char assert_size_u64_[(sizeof(unsigned long long) == 8) ? 1 : -1];
typedef char assert_size_ptr_[(sizeof(void*) == 8) ? 1 : -1];


typedef unsigned long long c_eight_bytes
    /* aliases the real state struct */
    __attribute((__may_alias__));

    /* expose in dll */
    __declspec(dllexport) // __declspec to work with msvc (as well as gcc).
    // teehee this is applied to c_execute.
    // i luv non-whitespace-aware languages.
    // shitass msvc using the opposite convention to gcc in terms of attr
    // placement tho.

// Returns null on success, otherwise a string error message.
const char* c_execute(c_eight_bytes* c_state)
    /* must be emmited */
    __attribute((__used__))
    __attribute((__externally_visible__));

// TODO: hash the believed state interpration and compare/check input and
// expected


#endif
