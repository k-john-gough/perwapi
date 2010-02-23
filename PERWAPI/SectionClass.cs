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
using System.Collections;


namespace QUT.PERWAPI
{

    /**************************************************************************/
    // PE File Section Descriptor
    /**************************************************************************/
    /// <summary>
    /// Descriptor for a Section in a PEFile  eg .text, .sdata
    /// </summary>
    internal class Section
    {
        internal static readonly uint relocPageSize = 4096;  // 4K pages for fixups

      /// <summary>
      /// Eight characters exactly, null padded if necessary.
      /// </summary>
        char[] name;
        string nameString;

      /// <summary>
      /// Total size of the section in bytes. If this value is 
      /// greater than SizeOFRawData the section is zero-padded.
      /// </summary>
        uint loadedSize = 0;

      /// <summary>
      /// Position in memory when loaded, relative to image base.
      /// </summary>
        uint loadedRVA = 0;

      /// <summary>
      /// Size of raw data in the section. Must be multiple of file alignment size.
      /// Can be smaller than loadedSize, or larger (as a result of alignment).
      /// </summary>
        uint sizeOnDisk = 0;

      /// <summary>
      /// Offset to section's page within the PE file.  Must be multiple
      /// of file alignment constant.
      /// </summary>
        uint fileOffset = 0;

      // These are all zero mostly.
        uint relocRVA, lineRVA, relocOff, numRelocs, numLineNums = 0;

      /// <summary>
      /// Flags of section: code = 0x20, init-data = 0x40, un-init-data = 0x80, 
      /// execute = 0x20000000, read = 0x40000000, write = 0x80000000.
      /// </summary>
        uint flags;

        uint relocTide = 0;
        uint padding = 0;
        uint[] relocs;


        internal Section(string sName, uint sFlags)
        {
            nameString = sName;
            name = sName.ToCharArray();
            flags = sFlags;
        }

        internal Section(PEReader input)
        {
            name = new char[8];
            for (int i = 0; i < name.Length; i++)
                name[i] = (char)input.ReadByte();
            nameString = new String(name);
            loadedSize = input.ReadUInt32();
            loadedRVA = input.ReadUInt32();
            sizeOnDisk = input.ReadUInt32();
            fileOffset = input.ReadUInt32();
            relocRVA = input.ReadUInt32();
            lineRVA = input.ReadUInt32();
            numRelocs = input.ReadUInt16();
            numLineNums = input.ReadUInt16();
            flags = input.ReadUInt32();
            if (Diag.DiagOn)
            {
                Console.WriteLine("  " + nameString + " RVA = " + Hex.Int(loadedRVA) + "  vSize = " + Hex.Int(loadedSize));
                Console.WriteLine("        FileOffset = " + Hex.Int(fileOffset) + "  aSize = " + Hex.Int(sizeOnDisk));
            }
        }

        internal bool ContainsRVA(uint rvaPos)
        {
            return (loadedRVA <= rvaPos) && (rvaPos <= loadedRVA + loadedSize);
        }

        internal uint GetOffset(uint inRVA)
        {
            uint offs = 0;
            if ((loadedRVA <= inRVA) && (inRVA <= loadedRVA + loadedSize))
                offs = fileOffset + (inRVA - loadedRVA);
            return offs;
        }

        internal uint Tide() { return loadedSize; }

        internal void IncTide(uint incVal) { loadedSize += incVal; }

        internal uint Padding() { return padding; }

        internal uint Size() { return sizeOnDisk; }

        internal void SetSize(uint pad)
        {
            padding = pad;
            sizeOnDisk = loadedSize + padding;
        }

        internal uint RVA() { return loadedRVA; }

        internal void SetRVA(uint loadedRVA) { this.loadedRVA = loadedRVA; }

        internal uint Offset() { return fileOffset; }

        internal void SetOffset(uint offs) { fileOffset = offs; }

        internal void DoBlock(BinaryWriter reloc, uint page, int start, int end)
        {
            //Console.WriteLine("rva = " + rva + "  page = " + page);
            if (Diag.DiagOn) Console.WriteLine("writing reloc block at " + reloc.BaseStream.Position);
            reloc.Write(loadedRVA + page);
            uint blockSize = (uint)(((end - start + 1) * 2) + 8);
            reloc.Write(blockSize);
            if (Diag.DiagOn) Console.WriteLine("Block size = " + blockSize);
            for (int j = start; j < end; j++)
            {
                //Console.WriteLine("reloc offset = " + relocs[j]);
                reloc.Write((ushort)((0x3 << 12) | (relocs[j] - page)));
            }
            reloc.Write((ushort)0);
            if (Diag.DiagOn) Console.WriteLine("finished reloc block at " + reloc.BaseStream.Position);
        }

        internal void DoRelocs(BinaryWriter reloc)
        {
            if (relocTide > 0)
            {
                // align block to 32 bit boundary
                relocOff = (uint)reloc.Seek(0, SeekOrigin.Current);
                if ((relocOff % 32) != 0)
                {
                    uint padding = 32 - (relocOff % 32);
                    for (int i = 0; i < padding; i++)
                        reloc.Write((byte)0);
                    relocOff += padding;
                }
                uint block = (relocs[0] / relocPageSize + 1) * relocPageSize;
                int start = 0;
                for (int i = 1; i < relocTide; i++)
                {
                    if (relocs[i] >= block)
                    {
                        DoBlock(reloc, block - relocPageSize, start, i);
                        start = i;
                        block = (relocs[i] / relocPageSize + 1) * relocPageSize;
                    }
                }
                DoBlock(reloc, block - relocPageSize, start, (int)relocTide);
            }
        }


        internal void AddReloc(uint offs)
        {
            if (Diag.DiagOn) Console.WriteLine("Adding a reloc to " + nameString + " section");
            int pos = 0;
            if (relocs == null)
            {
                relocs = new uint[5];
            }
            else
            {
                if (relocTide >= relocs.Length)
                {
                    uint[] tmp = relocs;
                    relocs = new uint[tmp.Length + 5];
                    for (int i = 0; i < relocTide; i++)
                    {
                        relocs[i] = tmp[i];
                    }
                }
                while ((pos < relocTide) && (relocs[pos] < offs)) pos++;
                for (int i = pos; i < relocTide; i++)
                {
                    relocs[i + 1] = relocs[i];
                }
            }
            relocs[pos] = offs;
            relocTide++;
            if (Diag.DiagOn) Console.WriteLine("relocTide = " + relocTide);
        }

        internal void WriteHeader(BinaryWriter output, uint relocRVA)
        {
            if (Diag.DiagOn) Console.WriteLine("relocTide = " + relocTide);
            output.Write(name);
            output.Write(loadedSize);                 // Virtual size
            output.Write(loadedRVA);                  // Virtual address
            output.Write(sizeOnDisk);                 // SizeOfRawData
            output.Write(fileOffset);               // PointerToRawData
            if (relocTide > 0)
            {
                output.Write(relocRVA + relocOff);
            }
            else
            {
                if (Diag.DiagOn) Console.WriteLine(nameString + " section has no relocs");
                output.Write(0);
            }                                   // PointerToRelocations
            output.Write(0);                    // PointerToLineNumbers
            output.Write((ushort)relocTide);    // NumberOfRelocations
            output.Write((ushort)0);            // NumberOfLineNumbers
            output.Write(flags);                // Characteristics
        }

    }


}