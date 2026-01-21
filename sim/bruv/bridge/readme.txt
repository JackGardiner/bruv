Basic idea:
  The c interacts with the outside via a "state array", an array of 8 byte values
  with some fixed interpretation (i.e. index 0 is an int, 1&2 are floats, 3 is a
  pointer, etc.). The only interaction the c has with this state is via a single
  entrypoint function (`c_execute`) from which it may read the given state as
  input and write to any entries as it pleases.

  The python needs to build and understand this state, which is facilitated
  through a bridge. This python will "build"/"know" its own interpretation
  independantly from the c (since communicating it out of the c is really hard
  lmao) but it MUST match (obviously).

  To enforce that these interpretations match, each interpretation will be hashed
  and then compared to ensure agreement. Note that this interpretation hash
  effectively enforces the contract between the python and c (this includes
  things such as the size of the state array, its ordering, etc.).

c:
  Exposes two things:
  - functions to facilitate making the interpretation hash.
  - an entrypoint which takes the state array + interpretation hash.

Bridge:
  The bridge is responsible for:
  - allowing the python to build an interpretation hash in the format the c
        expects.
  - allowing the python to read/write to the state array
  - handling the c entrypoint (i.e. invoking the c on the state array)

State array interpretation:
  Each element occupies 8B, just for simplicity. Having padding is ok as long as
  bridge facilitates it. However, we don't currently have any use of it. Each
  element is represented by three things:
  - name (string)
  - kind (enum value)
  - handling (flags)
  Note the c combines the last two into one "entry" int. idk cause why not.

  Possible kinds are:
  - 64bit floating point (IEEE-754)
  - 64bit signed integer (two's complement)
  - 64bit pointer to any of:
    - 32bit floating point (IEEE-754)
    - 64bit floating point (IEEE-754)
    - 8bit signed integer (two's complement)
    - 16bit signed integer (two's complement)
    - 32bit signed integer (two's complement)
    - 64bit signed integer (two's complement)
    - 8bit unsigned integer
    - 16bit unsigned integer
    - 32bit unsigned integer
    - 64bit unsigned integer
    Note this pointer does not store a size itself, the size is likely another
    distinct element of the state array or some fixed known size. Also note this
    pointer is allowed to be null if its element count is understood to be zero.

  Possible handling flags are (note these correspond to different things on
  different sides of the bridge):
  - INPUT (fontend must set to some valid value)
  - OUTPUT (backend will set/overwrite to some value)
  For following a pointers data (note this includes buffer moving/resizing):
  - INPUT_DATA
  - OUTPUT_DATA
  Note none of these flags are required or mutually exclusive, and all are set in
  addition to the type.
