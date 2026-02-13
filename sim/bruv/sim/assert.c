#include "assert.h"


i32 assertion_has_failed(void) {
    return (setjmp(assert_jump_) != 0);
}

const char* assertion_message(void) {
    return (const char*)assert_msg_;
}


typeof(assert_jump_) assert_jump_;
typeof(assert_msg_) assert_msg_;
