#pragma once
#include "br.h"

// Cheeky jumping-assert.

#define assertion_has_failed() ( setjmp(assert_jump_) )

#define assertion_message() ( (const char*)assert_msg_ )

#define assert(x, fmt, args...) do {                                            \
        if (__builtin_expect_with_probability(!(x), 0, 1.0)) {                  \
            int off = snprintf(assert_msg_, numel(assert_msg_),                 \
                    "!ERROR!  from \"%s\":%d\n", __FILE__, __LINE__);           \
            if (within(off, 0, numel(assert_msg_) - 2) {                        \
                int off2 = snprintf(assert_msg_ + off, numel(assert_msg_) - off \
                        (fmt), ##args);                                         \
                /* propogate error to off. */                                   \
                if (off2 < 0 || off + off2 > numel(assert_msg_) - 1)            \
                    off = numel(assert_msg_) + 1;                               \
            }                                                                   \
            if (!within(off, 0, numel(assert_msg_) - 1))                        \
                strncpy(assert_msg_, numel(assert_msg_),                        \
                        "christ it overflew (or errored)");                     \
            longjmp(assert_jump_, 1);                                           \
        }                                                                       \
    } while (0)


/* PRIVATE */

extern jmp_buf assert_jump_;
extern char assert_msg_[1024];
