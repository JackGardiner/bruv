// c my beloved.

// See ./readme.txt for an explanation.

#ifndef BRIDGE_H_
#define BRIDGE_H_


// bloody msvc.
#ifndef __GNUC__
  #define __attribute(...) /* ignored */
#endif

typedef char c_assert_size_f64_[(sizeof(double) == 8) ? 1 : -1];
typedef char c_assert_size_i64_[(sizeof(long long) == 8) ? 1 : -1];
typedef char c_assert_size_u64_[(sizeof(unsigned long long) == 8) ? 1 : -1];
typedef char c_assert_size_ptr_[(sizeof(void*) == 8) ? 1 : -1];

// Ensure the symbol is emitted in the final c lib (not bridge).
#define C_EMIT                                                      \
    __declspec(dllexport) /* to work with msvc (as well as gcc). */ \
    __attribute((__used__, __externally_visible__)) /* since we use godfile. */



/* INTERPRETATION HASH */

// Integer type to store the interpretation hash.
typedef unsigned long long c_IH;

// Enum type to represent an entry to add to a hash. The entry is a type combined
// any of the flags.
typedef unsigned int c_IHentry;

#define C_F64     ((c_IHentry)0x1)
#define C_I64     ((c_IHentry)0x2)
#define C_PTR_F32 ((c_IHentry)0x3)
#define C_PTR_F64 ((c_IHentry)0x4)
#define C_PTR_I8  ((c_IHentry)0x5)
#define C_PTR_I16 ((c_IHentry)0x6)
#define C_PTR_I32 ((c_IHentry)0x7)
#define C_PTR_I64 ((c_IHentry)0x8)
#define C_PTR_U8  ((c_IHentry)0x9)
#define C_PTR_U16 ((c_IHentry)0xA)
#define C_PTR_U32 ((c_IHentry)0xB)
#define C_PTR_U64 ((c_IHentry)0xC)

#define C_INPUT       ((c_IHentry)0x10U)
#define C_OUTPUT      ((c_IHentry)0x20U)
#define C_INPUT_DATA  ((c_IHentry)0x40U)
#define C_OUTPUT_DATA ((c_IHentry)0x80U)


// Initial value of an interpretation hash.
C_EMIT c_IH c_ih_initial(void);

// Appends the given node to the interpretation hash, returning the new running
// hash.
C_EMIT c_IH c_ih_append(c_IH running, const char* name, c_IHentry entry);



/* ENTRYPOINT */

typedef unsigned long long c_eight_bytes
    /* aliases the real state struct */
    __attribute((__may_alias__));

// Returns null on success, otherwise a string error message.
C_EMIT const char* c_execute(c_eight_bytes* state, c_IH interpretation_hash);


#endif
