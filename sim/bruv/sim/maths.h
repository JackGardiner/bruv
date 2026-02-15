#pragma once
#include "br.h"



// ========================= //
//         CONSTANTS         //
// ========================= //

// coupla constants.

#define PI     (3.141592653589793)  // pi
#define PI_2   (1.5707963267948966) // pi/2
#define PI_4   (0.7853981633974483) // pi/4
#define TWOPI  (6.283185307179586)  // 2*pi
#define EUL    (2.718281828459045)  // e
#define LN2    (0.6931471805599453) // log_e(2)
#define LN10   (2.302585092994046)  // log_e(10)
#define LOG2E  (1.4426950408889634) // log_2(e)
#define LOG10E (0.4342944819032518) // log_10(e)
#define SQRTH  (0.7071067811865476) // (1/2)^(1/2)
#define SQRT2  (1.4142135623730951) // 2^(1/2)
#define SQRT3  (1.7320508075688772) // 3^(1/2)
#define SQRT4  (2.0000000000000000) // 4^(1/2)
#define CBRTH  (0.7937005259840997) // (1/2)^(1/3)
#define CBRT2  (1.259921049894873)  // 2^(1/3)
#define CBRT3  (1.4422495703074083) // 3^(1/3)

#define fPI     (3.1415927f)  // 32b; pi
#define fPI_2   (1.5707964f)  // 32b; pi/2
#define fPI_4   (0.7853982f)  // 32b; pi/4
#define fTWOPI  (6.2831855f)  // 32b; 2*pi
#define fEUL    (2.7182817f)  // 32b; e
#define fLN2    (0.6931472f)  // 32b; log_e(2)
#define fLN10   (2.3025851f)  // 32b; log_e(10)
#define fLOG2E  (1.442695f)   // 32b; log_2(e)
#define fLOG10E (0.43429446f) // 32b; log_10(e)
#define fSQRTH  (0.70710677f) // 32b; (1/2)^(1/2)
#define fSQRT2  (1.4142135f)  // 32b; 2^(1/2)
#define fSQRT3  (1.7320508f)  // 32b; 3^(1/2)
#define fSQRT4  (2.0000000f)  // 32b; 4^(1/2)
#define fCBRTH  (0.7937005f)  // 32b; (1/2)^(1/3)
#define fCBRT2  (1.2599211f)  // 32b; 2^(1/3)
#define fCBRT3  (1.4422495f)  // 32b; 3^(1/3)

#define v2PI     (vec2(fPI))     // vec2; pi
#define v2PI_2   (vec2(fPI_2))   // vec2; pi/2
#define v2PI_4   (vec2(fPI_4))   // vec2; pi/4
#define v2TWOPI  (vec2(fTWOPI))  // vec2; 2*pi
#define v2EUL    (vec2(fEUL))    // vec2; e
#define v2LN2    (vec2(fLN2))    // vec2; log_e(2)
#define v2LN10   (vec2(fLN10))   // vec2; log_e(10)
#define v2LOG2E  (vec2(fLOG2E))  // vec2; log_2(e)
#define v2LOG10E (vec2(fLOG10E)) // vec2; log_10(e)
#define v2SQRTH  (vec2(fSQRTH))  // vec2; (1/2)^(1/2)
#define v2SQRT2  (vec2(fSQRT2))  // vec2; 2^(1/2)
#define v2SQRT3  (vec2(fSQRT3))  // vec2; 3^(1/2)
#define v2SQRT4  (vec2(fSQRT4))  // vec2; 4^(1/2)
#define v2CBRTH  (vec2(fCBRTH))  // vec2; (1/2)^(1/3)
#define v2CBRT2  (vec2(fCBRT2))  // vec2; 2^(1/3)
#define v2CBRT3  (vec2(fCBRT3))  // vec2; 3^(1/3)

#define v3PI     (vec3(fPI))     // vec3; pi
#define v3PI_2   (vec3(fPI_2))   // vec3; pi/2
#define v3PI_4   (vec3(fPI_4))   // vec3; pi/4
#define v3TWOPI  (vec3(fTWOPI))  // vec3; 2*pi
#define v3EUL    (vec3(fEUL))    // vec3; e
#define v3LN2    (vec3(fLN2))    // vec3; log_e(2)
#define v3LN10   (vec3(fLN10))   // vec3; log_e(10)
#define v3LOG2E  (vec3(fLOG2E))  // vec3; log_2(e)
#define v3LOG10E (vec3(fLOG10E)) // vec3; log_10(e)
#define v3SQRTH  (vec3(fSQRTH))  // vec3; (1/2)^(1/2)
#define v3SQRT2  (vec3(fSQRT2))  // vec3; 2^(1/2)
#define v3SQRT3  (vec3(fSQRT3))  // vec3; 3^(1/2)
#define v3SQRT4  (vec3(fSQRT4))  // vec3; 4^(1/2)
#define v3CBRTH  (vec3(fCBRTH))  // vec3; (1/2)^(1/3)
#define v3CBRT2  (vec3(fCBRT2))  // vec3; 2^(1/3)
#define v3CBRT3  (vec3(fCBRT3))  // vec3; 3^(1/3)

#define v4PI     (vec4(fPI))     // vec4; pi
#define v4PI_2   (vec4(fPI_2))   // vec4; pi/2
#define v4PI_4   (vec4(fPI_4))   // vec4; pi/4
#define v4TWOPI  (vec4(fTWOPI))  // vec4; 2*pi
#define v4EUL    (vec4(fEUL))    // vec4; e
#define v4LN2    (vec4(fLN2))    // vec4; log_e(2)
#define v4LN10   (vec4(fLN10))   // vec4; log_e(10)
#define v4LOG2E  (vec4(fLOG2E))  // vec4; log_2(e)
#define v4LOG10E (vec4(fLOG10E)) // vec4; log_10(e)
#define v4SQRTH  (vec4(fSQRTH))  // vec4; (1/2)^(1/2)
#define v4SQRT2  (vec4(fSQRT2))  // vec4; 2^(1/2)
#define v4SQRT3  (vec4(fSQRT3))  // vec4; 3^(1/2)
#define v4SQRT4  (vec4(fSQRT4))  // vec4; 4^(1/2)
#define v4CBRTH  (vec4(fCBRTH))  // vec4; (1/2)^(1/3)
#define v4CBRT2  (vec4(fCBRT2))  // vec4; 2^(1/3)
#define v4CBRT3  (vec4(fCBRT3))  // vec4; 3^(1/3)



// ========================== //
//         ELEMENTARY         //
// ========================== //

// TODO: maths functions (sin/log/etc.).


// ========================== //
//       LINEAR ALGEBRA       //
// ========================== //

// TODO: linalg/vector functions (dot/mag/proj/etc.).




// ========================== //
//           DETAIL           //
// ========================== //
