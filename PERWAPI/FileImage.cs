/*  
 * PERWAPI - An API for Reading and Writing PE Files
 * 
 * Copyright (c) Diane Corney, Queensland University of Technology, 2004.
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
 */

using System;
using System.IO;

namespace QUT.PERWAPI
{

    /**************************************************************************/
    // Class of PEFile constant values
    /**************************************************************************/
    /// <summary>
    /// Image for a PEFile
    /// File Structure
    ///     DOS Header (128 bytes) 
    ///     PE Signature ("PE\0\0") 
    ///     PEFileHeader (20 bytes)
    ///     PEOptionalHeader (224 bytes) 
    ///     SectionHeaders (40 bytes * NumSections)
    ///
    ///     Sections .text (always present - contains metadata)
    ///              .sdata (contains any initialised data in the file - may not be present)
    ///                     (for ilams /debug this contains the Debug table)
    ///              .reloc (always present - in pure CIL only has one fixup)
    ///               others???  c# produces .rsrc section containing a Resource Table
    ///
    /// .text layout
    ///     IAT (single entry 8 bytes for pure CIL)
    ///     CLIHeader (72 bytes)
    ///     CIL instructions for all methods (variable size)
    ///     MetaData 
    ///       Root (20 bytes + UTF-8 Version String + quad align padding)
    ///       StreamHeaders (8 bytes + null terminated name string + quad align padding)
    ///       Streams 
    ///         #~        (always present - holds metadata tables)
    ///         #Strings  (always present - holds identifier strings)
    ///         #US       (Userstring heap)
    ///         #Blob     (signature blobs)
    ///         #GUID     (guids for assemblies or Modules)
    ///    ImportTable (40 bytes)
    ///    ImportLookupTable(8 bytes) (same as IAT for standard CIL files)
    ///    Hint/Name Tables with entry "_CorExeMain" for .exe file and "_CorDllMain" for .dll (14 bytes)
    ///    ASCII string "mscoree.dll" referenced in ImportTable (+ padding = 16 bytes)
    ///    Entry Point  (0xFF25 followed by 4 bytes 0x400000 + RVA of .text)
    ///
    ///  #~ stream structure
    ///    Header (24 bytes)
    ///    Rows   (4 bytes * numTables)
    ///    Tables
    /// </summary>
    internal class FileImage
    {
        internal readonly static uint DelaySignSize = 128; // Current assemblies are always 128
        internal readonly static uint[] iByteMask = { 0x000000FF, 0x0000FF00, 0x00FF0000, 0xFF000000 };
        internal readonly static ulong[] lByteMask = {0x00000000000000FF, 0x000000000000FF00,
                                                         0x0000000000FF0000, 0x00000000FF000000,
                                                         0x000000FF00000000, 0x0000FF0000000000,
                                                         0x00FF000000000000, 0xFF00000000000000 };
        internal readonly static uint nibble0Mask = 0x0000000F;
        internal readonly static uint nibble1Mask = 0x000000F0;

        internal static readonly byte[] DOSHeader = { 0x4d,0x5a,0x90,0x00,0x03,0x00,0x00,0x00,
                                                        0x04,0x00,0x00,0x00,0xff,0xff,0x00,0x00,
                                                        0xb8,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x40,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x00,0x00,0x00,0x00,0x80,0x00,0x00,0x00,
                                                        0x0e,0x1f,0xba,0x0e,0x00,0xb4,0x09,0xcd,
                                                        0x21,0xb8,0x01,0x4c,0xcd,0x21,0x54,0x68,
                                                        0x69,0x73,0x20,0x70,0x72,0x6f,0x67,0x72,
                                                        0x61,0x6d,0x20,0x63,0x61,0x6e,0x6e,0x6f,
                                                        0x74,0x20,0x62,0x65,0x20,0x72,0x75,0x6e,
                                                        0x20,0x69,0x6e,0x20,0x44,0x4f,0x53,0x20,
                                                        0x6d,0x6f,0x64,0x65,0x2e,0x0d,0x0d,0x0a,
                                                        0x24,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
                                                        0x50,0x45,0x00,0x00};
        internal static readonly int PESigOffset = 0x3C;
        internal static byte[] PEHeader = { 0x4c, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                                              0xE0, 0x00, 0x0E, 0x01, // PE Header Standard Fields
                                              0x0B, 0x01, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 
                                              0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                                          };

        internal static IType[] instrMap = { 
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x00 - 0x08
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.uint8Op, IType.uint8Op, // 0x09 - 0x0F
                                               IType.uint8Op, IType.uint8Op, IType.uint8Op, IType.uint8Op,   // 0x10 - 0x13
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x14 - 0x1C
                                               IType.op, IType.op, IType.int8Op, IType.int32Op, IType.specialOp,   // 0x1D - 0x21
                                               IType.specialOp, IType.specialOp,IType.op,IType.op,IType.op,IType.methOp, // 0x22 - 0x27
                                               IType.methOp, IType.specialOp, IType.op, IType.branchOp, IType.branchOp,// 0x28 - 0x2C
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,       // 0x2D - 0x30
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,       // 0x31 - 0x34
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,      // 0x35 - 0x38
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,   // 0x39 - 0x3C
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,   // 0x3D - 0x40
                                               IType.branchOp, IType.branchOp, IType.branchOp, IType.branchOp,   // 0x41 - 0x44
                                               IType.specialOp, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,    // 0x45 - 0x4B
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x4C - 0x54
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x55 - 0x5D
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x5E - 0x66
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,       // 0x67 - 0x6E
                                               IType.methOp, IType.typeOp, IType.typeOp, IType.specialOp,     // 0x6F - 0x72
                                               IType.methOp, IType.typeOp, IType.typeOp, IType.op, IType.op, IType.op,   // 0x73 - 0x78
                                               IType.typeOp, IType.op, IType.fieldOp, IType.fieldOp, IType.fieldOp,// 0x79 - 0x7D
                                               IType.fieldOp, IType.fieldOp, IType.fieldOp, IType.typeOp,    // 0x7E - 0x81
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x82 - 0x8A
                                               IType.op, IType.typeOp, IType.typeOp, IType.op, IType.typeOp, IType.op,   // 0x8B - 0x90
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x91 - 0x99
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0x9A - 0xA2
                                               IType.typeOp, IType.typeOp, IType.typeOp, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0xA3 - 0xAB
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0xAC - 0xB4
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0xB5 - 0xBD
                                               IType.op, IType.op, IType.op, IType.op, IType.typeOp, IType.op, IType.op, IType.op,   // 0xBE - 0xC5
                                               IType.typeOp, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,   // 0xC6 - 0xCD
                                               IType.op, IType.op, IType.specialOp, IType.op, IType.op, IType.op, IType.op,    // 0xCE - 0xD4
                                               IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.op,       // 0xD5 - 0xDC
                                               IType.branchOp, IType.branchOp, IType.op, IType.op };                            // 0xDD - 0xE0

        internal static IType[] longInstrMap = { IType.op, IType.op, IType.op, IType.op, IType.op, IType.op, IType.methOp,   // 0x00 - 0x06
                                                   IType.methOp, IType.uint16Op, IType.uint16Op,       // 0x07 - 0x09
                                                   IType.uint16Op, IType.uint16Op, IType.uint16Op,     // 0x0A - 0x0C
                                                   IType.uint16Op, IType.uint16Op, IType.op, IType.op, IType.op,   // 0x0D - 0x11
                                                   IType.uint8Op, IType.op, IType.op, IType.typeOp, IType.typeOp, IType.op,  // 0x12 - 0x17
                                                   IType.op, IType.op, IType.op, IType.op, IType.typeOp, IType.op, IType.op};  // 0x18 - 0x1D

        internal static readonly uint bitmask0 = 0x00000001;
        internal static readonly uint bitmask1 = 0x00000002;
        internal static readonly uint bitmask2 = 0x00000004;
        internal static readonly uint bitmask3 = 0x00000008;
        internal static readonly uint bitmask4 = 0x00000010;
        internal static readonly uint bitmask5 = 0x00000020;
        internal static readonly uint bitmask6 = 0x00000040;
        internal static readonly uint bitmask7 = 0x00000080;
        internal static readonly uint bitmask8 = 0x00000100;
        internal static readonly uint bitmask9 = 0x00000200;
        internal static readonly uint bitmask10 = 0x00000400;
        internal static readonly uint bitmask11 = 0x00000800;
        internal static readonly uint bitmask12 = 0x00001000;
        internal static readonly uint bitmask13 = 0x00002000;
        internal static readonly uint bitmask14 = 0x00004000;
        internal static readonly uint bitmask15 = 0x00008000;
        internal static readonly uint bitmask16 = 0x00010000;
        internal static readonly uint bitmask17 = 0x00020000;
        internal static readonly uint bitmask18 = 0x00040000;
        internal static readonly uint bitmask19 = 0x00080000;
        internal static readonly uint bitmask20 = 0x00100000;
        internal static readonly uint bitmask21 = 0x00200000;
        internal static readonly uint bitmask22 = 0x00400000;
        internal static readonly uint bitmask23 = 0x00800000;
        internal static readonly uint bitmask24 = 0x01000000;
        internal static readonly uint bitmask25 = 0x02000000;
        internal static readonly uint bitmask26 = 0x04000000;
        internal static readonly uint bitmask27 = 0x08000000;
        internal static readonly uint bitmask28 = 0x10000000;
        internal static readonly uint bitmask29 = 0x20000000;
        internal static readonly uint bitmask30 = 0x40000000;
        internal static readonly uint bitmask31 = 0x80000000;
        internal static readonly ulong bitmask32 = 0x0000000100000000;
        internal static readonly ulong bitmask33 = 0x0000000200000000;
        internal static readonly ulong bitmask34 = 0x0000000400000000;
        internal static readonly ulong bitmask35 = 0x0000000800000000;
        internal static readonly ulong bitmask36 = 0x0000001000000000;
        internal static readonly ulong bitmask37 = 0x0000002000000000;
        internal static readonly ulong bitmask38 = 0x0000004000000000;
        internal static readonly ulong bitmask39 = 0x0000008000000000;
        internal static readonly ulong bitmask40 = 0x0000010000000000;
        internal static readonly ulong bitmask41 = 0x0000020000000000;
        internal static readonly ulong bitmask42 = 0x0000040000000000;
        internal static readonly ulong bitmask43 = 0x0000080000000000;
        internal static readonly ulong bitmask44 = 0x0000100000000000;
        internal static readonly ulong bitmask45 = 0x0000200000000000;
        internal static readonly ulong bitmask46 = 0x0000400000000000;
        internal static readonly ulong bitmask47 = 0x0000800000000000;
        internal static readonly ulong bitmask48 = 0x0001000000000000;
        internal static readonly ulong bitmask49 = 0x0002000000000000;
        internal static readonly ulong bitmask50 = 0x0004000000000000;
        internal static readonly ulong bitmask51 = 0x0008000000000000;
        internal static readonly ulong bitmask52 = 0x0010000000000000;
        internal static readonly ulong bitmask53 = 0x0020000000000000;
        internal static readonly ulong bitmask54 = 0x0040000000000000;
        internal static readonly ulong bitmask55 = 0x0080000000000000;
        internal static readonly ulong bitmask56 = 0x0100000000000000;
        internal static readonly ulong bitmask57 = 0x0200000000000000;
        internal static readonly ulong bitmask58 = 0x0400000000000000;
        internal static readonly ulong bitmask59 = 0x0800000000000000;
        internal static readonly ulong bitmask60 = 0x1000000000000000;
        internal static readonly ulong bitmask61 = 0x2000000000000000;
        internal static readonly ulong bitmask62 = 0x4000000000000000;
        internal static readonly ulong bitmask63 = 0x8000000000000000;

        internal static readonly ulong[] bitmasks = { bitmask0 , bitmask1 , bitmask2 , bitmask3 ,
                                                        bitmask4 , bitmask5 , bitmask6 , bitmask7 ,
                                                        bitmask8 , bitmask9 , bitmask10, bitmask11,
                                                        bitmask12, bitmask13, bitmask14, bitmask15,
                                                        bitmask16, bitmask17, bitmask18, bitmask19,
                                                        bitmask20, bitmask21, bitmask22, bitmask23,
                                                        bitmask24, bitmask25, bitmask26, bitmask27,
                                                        bitmask28, bitmask29, bitmask30, bitmask31,
                                                        bitmask32, bitmask33, bitmask34, bitmask35,
                                                        bitmask36, bitmask37, bitmask38, bitmask39,
                                                        bitmask40, bitmask41, bitmask42, bitmask43,
                                                        bitmask44, bitmask45, bitmask46, bitmask47,
                                                        bitmask48, bitmask49, bitmask50, bitmask51,
                                                        bitmask52, bitmask53, bitmask54, bitmask55,
                                                        bitmask56, bitmask57, bitmask58, bitmask59,
                                                        bitmask60, bitmask61, bitmask62, bitmask63 };


        internal static readonly uint TableMask = 0xFF000000;
        internal static readonly uint ElementMask = 0x00FFFFFF;
        internal static readonly int NAMELEN = 8, STRLEN = 200;
        internal static readonly uint machine = 0x14C;
        internal static readonly uint machinex64 = 0x8664;
        internal static readonly uint magic = 0x10B;
        internal static readonly uint magic64 = 0x20B;
        internal static readonly uint minFileAlign = 0x200;
        internal static readonly uint midFileAlign = 0x400;
        internal static readonly uint maxFileAlign = 0x1000;
        internal static readonly uint fileHeaderSize = 0x178;
        internal static readonly uint sectionHeaderSize = 40;
        internal static readonly uint SectionAlignment = 0x2000;
        internal static readonly uint ImageBase = 0x400000;
        internal static readonly uint ImportTableSize = 40;
        internal static readonly uint IATSize = 8;
        internal static readonly uint CLIHeaderSize = 72;
        internal static readonly uint relocFlags = 0x42000040;
        internal static readonly ushort exeCharacteristics = 0x010E;
        internal static readonly ushort dllCharacteristics = 0x210E;
        internal static readonly ushort dllFlag = 0x2000;
        // section names are all 8 bytes
        internal static readonly string textName = ".text\0\0\0";
        internal static readonly string sdataName = ".sdata\0\0";
        internal static readonly string relocName = ".reloc\0\0";
        internal static readonly string rsrcName = ".rsrc\0\0\0";
        internal static readonly string exeHintNameTable = "\0\0_CorExeMain\0";
        internal static readonly string dllHintNameTable = "\0\0_CorDllMain\0";
        internal static readonly string runtimeEngineName = "mscoree.dll\0\0";
        internal static readonly DateTime origin = new DateTime(1970, 1, 1);
        internal static readonly ushort DLLFlags = (ushort)0x400; // for ver 1.1.4322 prev = (short)0;
        internal static readonly uint StackReserveSize = 0x100000;
        internal static readonly uint StackCommitSize = 0x1000;
        internal static readonly uint HeapReserveSize = 0x100000;
        internal static readonly uint HeapCommitSize = 0x1000;
        internal static readonly uint LoaderFlags = 0;
        internal static readonly uint NumDataDirectories = 0x10;
    }
}
