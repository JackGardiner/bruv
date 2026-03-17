#include "assert.h"


const char* assertion_message(void) {
    return (const char*)assert_msg_;
}


typeof(assert_jump_) assert_jump_;
typeof(assert_msg_) assert_msg_;
