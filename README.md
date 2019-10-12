# Intro

In various places, a PKWare library is used (often called implode.dll)
that implements an uncommon, proprietary compression algorithm.

This algorithm was reverse-engineered by Ben Rudiak-Gould back in 2001, and 
Mark Adler wrote a C implementation in 2003.

This project is a translation into C# of the PKWare compression algorithm 
implemented in C by [Mark Adler](https://github.com/madler/). 

This implementation varies from the C original. 

# Variations

I found that the files compressed in the legacy system I came into contact with
contained multiple, separate, compressed streams. Each stream is delineated by
an end of stream marker. Where the base implementation stops at the end of stream,
this implementation checks for further data on the stream and will start
decompressing again if it finds any.

# Implementation

This is probably the least elegant way of implementing a decompressor. It would
be much better to implement Stream, for example. However, it does what it says
on the tin; it decompresses the stream. If this was being used for something
that was important, you might go to the trouble of making it work better. For
me, though, this works faster than shelling out to an executable as the previous system did.

# Usage
```
new Utils.Blast(sourceStream, destinationStream).Decompress();
```

# Format notes

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