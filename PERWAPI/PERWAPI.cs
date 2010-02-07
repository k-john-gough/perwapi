/*  
 * PERWAPI - An API for Reading and Writing PE Files
 * 
 * Copyright (c) Diane Corney, Queensland University of Technology, 2004-2010.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the PERWAPI Copyright as included with this
 * distribution in the file PERWAPIcopyright.rtf.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY as is explained in the copyright notice.
 *
 * The author may be contacted at d.corney@qut.edu.au
 * 
 * Version Date:  26/01/07
 * 
 * Contributions Made By:
 * 
 * Douglas Stockwell - Developed support for PDB files.
 * Andrew Bacon - Integrated PDB file support and developed automatic 
 *                stack depth calculations.
 * 
 */

// The conditional compilation on the CORAPI symbol has been
// deleted from this version.  Go back to version 1 in the QUT SVN
// repository to get the conditional code if needed.

using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Diagnostics;
using System.Reflection; // For the assembly attributes

//
//  The assembly version is managed manually here.
//  1.1.* are versions with the PDB handling built 
//  Version 1.1.1+ uses System.Security.Crytography
//  to provide public key token methods
//
namespace QUT.PERWAPI {
  internal class MetaDataTables {
    private TableRow[][] tables;

    internal MetaDataTables(TableRow[][] tabs) {
      tables = tabs;
    }

    internal MetaDataElement GetTokenElement(uint token) {
      uint tabIx = (token & FileImage.TableMask) >> 24;
      uint elemIx = (token & FileImage.ElementMask) - 1;
      return (MetaDataElement)tables[tabIx][(int)elemIx];
    }

  }

  /**************************************************************************/
  // Streams for PE File Reader
  /**************************************************************************/
  /// <summary>
  /// Stream in the Meta Data  (#Strings, #US, #Blob and #GUID)
  /// </summary>
  /// 

  internal class MetaDataInStream : BinaryReader {
    //protected bool largeIx = false;
    protected byte[] data;

    public MetaDataInStream(byte[] streamBytes)
      : base(new MemoryStream(streamBytes)) {
      data = streamBytes;
    }

    public uint ReadCompressedNum() {
      //int pos = (int)BaseStream.Position;
      //Console.WriteLine("Position = " + BaseStream.Position);
      byte b = ReadByte();
      //pos++;
      uint num = 0;
      if (b <= 0x7F) {
        num = b;
      }
      else if (b >= 0xC0) {
        num = (uint)(((b - 0xC0) << 24) + (ReadByte() << 16) + (ReadByte() << 8) + ReadByte());
      }
      else { // (b >= 0x80) && (b < 0xC0)
        num = (uint)((b - 0x80) << 8) + ReadByte();
      }
      return num;
    }

    public int ReadCompressedInt() {
      // This code is based on a revised version of the
      // (incorrect) ECMA-335 spec which clarifies the 
      // encoding of array lower bounds. (kjg 2008-Feb-22 )
      //
      uint rawBits = ReadCompressedNum();
      uint magnitude = (rawBits >> 1);
      if ((rawBits & 1) != 1)
        return (int)magnitude;
      else if (magnitude <= 0x3f)
        return (int)(magnitude | 0xffffffc0);
      else if (magnitude <= 0x1fff)
        return (int)(magnitude | 0xffffe000);
      else
        return (int)(magnitude | 0xf0000000);
    }

    internal bool AtEnd() {
      long pos = BaseStream.Position;
      long len = BaseStream.Length;
      //if (pos >= len-1)
      //  Console.WriteLine("At end of stream");
      return BaseStream.Position == BaseStream.Length - 1;
    }

    internal byte[] GetBlob(uint ix) {
      if (ix == 0) return new byte[0];
      BaseStream.Seek(ix, SeekOrigin.Begin);
      //Console.WriteLine("Getting blob size at index " + buff.GetPos());
      //if (Diag.CADiag) Console.WriteLine("Getting blob size at " + (BaseStream.Position+PEReader.blobStreamStartOffset));
      uint bSiz = ReadCompressedNum();
      //byte[] blobBytes = new byte[ReadCompressedNum()];
      //if (Diag.CADiag) Console.WriteLine("Blob size =  " + bSiz);
      byte[] blobBytes = new byte[bSiz];
      for (int i = 0; i < blobBytes.Length; i++) {
        blobBytes[i] = ReadByte();
      }
      return blobBytes;
    }

    internal byte[] GetBlob(uint ix, int len) {
      //Console.WriteLine("Getting blob size at index " + buffer.GetPos());
      byte[] blobBytes = new byte[len];
      for (int i = 0; i < len; i++) {
        blobBytes[i] = data[ix++];
      }
      return blobBytes;
    }

    internal string GetString(uint ix) {
      uint end;
      for (end = ix; data[end] != '\0'; end++) ;
      char[] str = new char[end - ix];
      for (int i = 0; i < str.Length; i++) {
        str[i] = (char)data[ix + i];
      }
      return new string(str, 0, str.Length);
    }

    internal string GetBlobString(uint ix) {
      if (ix == 0) return "";
      BaseStream.Seek(ix, SeekOrigin.Begin);
      return GetBlobString();
    }

    internal string GetBlobString() {
      uint strLen = ReadCompressedNum();
      char[] str = new char[strLen];
      uint readpos = (uint)this.BaseStream.Position;
      for (int i = 0; i < strLen; i++) {
        str[i] = ReadChar();
        uint newpos = (uint)this.BaseStream.Position;
        if (newpos > readpos + 1)
          strLen -= newpos - (readpos + 1);
        readpos = newpos;
      }
      return new string(str, 0, (int)strLen);
    }

    internal void GoToIndex(uint ix) {
      BaseStream.Seek(ix, SeekOrigin.Begin);
    }

  }
  /**************************************************************************/

  internal class MetaDataStringStream : BinaryReader {
    //BinaryReader br;

    internal MetaDataStringStream(byte[] bytes)
      : base(new MemoryStream(bytes), Encoding.Unicode) {
      //br = new BinaryReader(new MemoryStream(bytes)/*,Encoding.Unicode*/);
    }

    private uint GetStringLength() {
      uint b = ReadByte();
      uint num = 0;
      if (b <= 0x7F) {
        num = b;
      }
      else if (b >= 0xC0) {
        num = (uint)(((b - 0xC0) << 24) + (ReadByte() << 16) + (ReadByte() << 8) + ReadByte());
      }
      else { // (b >= 0x80) && (b < 0xC0)
        num = (uint)((b - 0x80) << 8) + ReadByte();
      }
      return num;
    }

    internal string GetUserString(uint ix) {
      BaseStream.Seek(ix, SeekOrigin.Begin);
      uint strLen = GetStringLength() / 2;
      char[] strArray = new char[strLen];
      for (int i = 0; i < strLen; i++) {
        //strArray[i] = ReadChar(); // works for everett but not whidbey
        strArray[i] = (char)ReadUInt16();
      }
      return new String(strArray);
    }

  }


  /**************************************************************************/
  // Streams for generated MetaData
  /**************************************************************************/
  /// <summary>
  /// Stream in the generated Meta Data  (#Strings, #US, #Blob and #GUID)
  /// </summary>
  internal class MetaDataStream : BinaryWriter {
    private static readonly uint StreamHeaderSize = 8;
    private static uint maxSmlIxSize = 0xFFFF;

    private uint start = 0;
    uint size = 0, tide = 1;
    bool largeIx = false;
    uint sizeOfHeader;
    internal char[] name;
    Hashtable htable = new Hashtable();

    internal MetaDataStream(char[] name, bool addInitByte)
      : base(new MemoryStream()) {
      if (addInitByte) { Write((byte)0); size = 1; }
      this.name = name;
      sizeOfHeader = StreamHeaderSize + (uint)name.Length;
    }

    internal MetaDataStream(char[] name, System.Text.Encoding enc, bool addInitByte)
      : base(new MemoryStream(), enc) {
      if (addInitByte) { Write((byte)0); size = 1; }
      this.name = name;
      sizeOfHeader = StreamHeaderSize + (uint)name.Length;
    }

    public uint Start {
      get {
        return start;
      }
      set {
        start = value;
      }
    }

    internal uint headerSize() {
      // Console.WriteLine(name + " stream has headersize of " + sizeOfHeader);
      return sizeOfHeader;
    }

    //internal void SetSize(uint siz) {
    //  size = siz;
    //}

    internal uint Size() {
      return size;
    }

    internal bool LargeIx() {
      return largeIx;
    }

    internal void WriteDetails() {
      // Console.WriteLine(name + " - size = " + size);
    }

    internal uint Add(string str, bool prependSize) {
      Object val = htable[str];
      uint index = 0;
      if (val == null) {
        index = size;
        htable[str] = index;
        char[] arr = str.ToCharArray();
        if (prependSize)
          CompressNum((uint)arr.Length * 2 + 1);
        Write(arr);
        Write((byte)0);
        size = (uint)Seek(0, SeekOrigin.Current);
      }
      else {
        index = (uint)val;
      }
      return index;
    }

    internal uint Add(Guid guid) {
      Write(guid.ToByteArray());
      size = (uint)Seek(0, SeekOrigin.Current);
      return tide++;
    }

    internal uint Add(byte[] blob) {
      uint ix = size;
      CompressNum((uint)blob.Length);
      Write(blob);
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    internal uint Add(long val, uint numBytes) {
      uint ix = size;
      Write((byte)numBytes);
      switch (numBytes) {
        case 1: Write((byte)val); break;
        case 2: Write((short)val); break;
        case 4: Write((int)val); break;
        default: Write(val); break;
      }
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    internal uint Add(ulong val, uint numBytes) {
      uint ix = size;
      Write((byte)numBytes);
      switch (numBytes) {
        case 1: Write((byte)val); break;
        case 2: Write((ushort)val); break;
        case 4: Write((uint)val); break;
        default: Write(val); break;
      }
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    internal uint Add(char ch) {
      uint ix = size;
      Write((byte)2);  // size of blob to follow
      Write(ch);
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    internal uint Add(float val) {
      uint ix = size;
      Write((byte)4);  // size of blob to follow
      Write(val);
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    internal uint Add(double val) {
      uint ix = size;
      Write((byte)8);  // size of blob to follow
      Write(val);
      size = (uint)Seek(0, SeekOrigin.Current);
      return ix;
    }

    private void CompressNum(uint val) {
      if (val <= 0x7F) {
        Write((byte)val);
      }
      else if (val <= 0x3FFF) {
        byte b1 = (byte)((val >> 8) | 0x80);
        byte b2 = (byte)(val & FileImage.iByteMask[0]);
        Write(b1);
        Write(b2);
      }
      else {
        byte b1 = (byte)((val >> 24) | 0xC0);
        byte b2 = (byte)((val & FileImage.iByteMask[2]) >> 16);
        byte b3 = (byte)((val & FileImage.iByteMask[1]) >> 8); ;
        byte b4 = (byte)(val & FileImage.iByteMask[0]);
        Write(b1);
        Write(b2);
        Write(b3);
        Write(b4);
      }
    }

    private void QuadAlign() {
      if ((size % 4) != 0) {
        uint pad = 4 - (size % 4);
        size += pad;
        for (int i = 0; i < pad; i++) {
          Write((byte)0);
        }
      }
    }

    internal void EndStream() {
      QuadAlign();
      if (size > maxSmlIxSize) {
        largeIx = true;
      }
    }

    internal void WriteHeader(BinaryWriter output) {
      output.Write(start);
      output.Write(size);
      output.Write(name);
    }

    internal virtual void Write(BinaryWriter output) {
      // Console.WriteLine("Writing " + name + " stream at " + output.Seek(0,SeekOrigin.Current) + " = " + start);
      MemoryStream str = (MemoryStream)BaseStream;
      output.Write(str.ToArray());
    }

  }


}



