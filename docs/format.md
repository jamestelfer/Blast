# Blast (explode/implode) format notes

> _These notes are taken from the `blast.c` reference code written by [Mark Adler](https://github.com/madler/)._

- First byte is 0 if literals are uncoded or 1 if they are coded.  Second
  byte is 4, 5, or 6 for the number of extra bits in the distance code.
  This is the base-2 logarithm of the dictionary size minus six.

- Compressed data is a combination of literals and length/distance pairs
  terminated by an end code.  Literals are either Huffman coded or
  uncoded bytes.  A length/distance pair is a coded length followed by a
  coded distance to represent a string that occurs earlier in the
  uncompressed data that occurs again at the current location.

- A bit preceding a literal or length/distance pair indicates which comes
  next, 0 for literals, 1 for length/distance.

- If literals are uncoded, then the next eight bits are the literal, in the
  normal bit order in th stream, i.e. no bit-reversal is needed. Similarly,
  no bit reversal is needed for either the length extra bits or the distance
  extra bits.

- Literal bytes are simply written to the output.  A length/distance pair is
  an instruction to copy previously uncompressed bytes to the output.  The
  copy is from distance bytes back in the output stream, copying for length
  bytes.

- Distances pointing before the beginning of the output data are not
  permitted.

- Overlapped copies, where the length is greater than the distance, are
  allowed and common.  For example, a distance of one and a length of 518
  simply copies the last byte 518 times.  A distance of four and a length of
  twelve copies the last four bytes three times.  A simple forward copy
  ignoring whether the length is greater than the distance or not implements
  this correctly.

## Huffman-encoded data

Literals and length/distance pairs can be Huffman encoded; these codes
are read from the bitstream and interpreted by one of the Huffman
tables that are included in literal form in the code.

- The codes as stored in the compressed data are bit-reversed relative to
  a simple integer ordering of codes of the same lengths. Hence the
  bits are pulled from the compressed data one at a time and used to
  build the code value reversed from what is in the stream in order to
  permit simple integer comparisons for decoding.

- The first code for the shortest length is all ones. Subsequent codes of
  the same length are simply integer decrements of the previous code.  When
  moving up a length, a one bit is appended to the code. For a complete
  code, the last code of the longest length will be all zeros. To support
  this ordering, the bits pulled during decoding are inverted to apply the
  more "natural" ordering starting with all zeros and incrementing.
