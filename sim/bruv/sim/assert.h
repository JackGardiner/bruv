#pragma once
#include "br.h"


// Cheeky jumping-assert.


// `setjmp` wrapper, sets up assertions to jump to here on failure and returns
// non-zero if any assertion fails.
i32 assertion_has_failed(void);

// Returns the message of an assert. Only valid if an assert has failed.
const char* assertion_message(void);

// Asserts that `x` is non-zero. If `x` is zero, the assertion fails and the most
// recent call of `assertion_has_failed` is jumped to, with `fmt_and_args` parsed
// in a printf-manner and used as the error message.
#define assert(x, fmt_and_args...) do {                                         \
        if (__builtin_expect_with_probability(!(x), 0, 1.0)) {                  \
            int off = snprintf(assert_msg_, numel(assert_msg_),                 \
                    "ERROR, from \"%s\":%d\n", __FILE__, __LINE__);             \
            if (0 <= off && off <= numel(assert_msg_) - 2) {                    \
                int off2 = snprintf(assert_msg_ + off, numel(assert_msg_) - off,\
                        fmt_and_args);                                          \
                /* propogate error to off. */                                   \
                if (off2 < 0 || off + off2 > numel(assert_msg_) - 1)            \
                    off = -1;                                                   \
            } else off = -1;                                                    \
            if (!(0 <= off && off <= numel(assert_msg_) - 1)) {                 \
                __builtin_strncpy(assert_msg_, "christ it overflew (or "        \
                        "errored)", numel(assert_msg_));                        \
            }                                                                   \
            longjmp(assert_jump_, 1);                                           \
        }                                                                       \
    } while (0)



/* PRIVATE */

extern jmp_buf assert_jump_;
extern char assert_msg_[1024];
