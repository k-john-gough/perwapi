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

        char[] name;
        string nameString;
        uint offset = 0, tide = 0, size = 0, rva = 0, relocTide = 0, numRelocs = 0;
        uint relocOff = 0, relocRVA = 0, lineRVA = 0, numLineNums = 0;
        uint flags = 0, padding = 0;
        uint[] relocs;
        //bool relocsDone = false;

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
            tide = input.ReadUInt32();
            rva = input.ReadUInt32();
            size = input.ReadUInt32();
            offset = input.ReadUInt32();
            relocRVA = input.ReadUInt32();
            lineRVA = input.ReadUInt32();
            numRelocs = input.ReadUInt16();
            numLineNums = input.ReadUInt16();
            flags = input.ReadUInt32();
            if (Diag.DiagOn)
            {
                Console.WriteLine("  " + nameString + " RVA = " + Hex.Int(rva) + "  vSize = " + Hex.Int(tide));
                Console.WriteLine("        FileOffset = " + Hex.Int(offset) + "  aSize = " + Hex.Int(size));
            }
        }

        internal bool ContainsRVA(uint rvaPos)
        {
            return (rva <= rvaPos) && (rvaPos <= rva + tide);
        }
        internal uint GetOffset(uint inRVA)
        {
            uint offs = 0;
            if ((rva <= inRVA) && (inRVA <= rva + tide))
                offs = offset + (inRVA - rva);
            return offs;
        }
        internal uint Tide() { return tide; }

        internal void IncTide(uint incVal) { tide += incVal; }

        internal uint Padding() { return padding; }

        internal uint Size() { return size; }

        internal void SetSize(uint pad)
        {
            padding = pad;
            size = tide + padding;
        }

        internal uint RVA() { return rva; }

        internal void SetRVA(uint rva) { this.rva = rva; }

        internal uint Offset() { return offset; }

        internal void SetOffset(uint offs) { offset = offs; }

        internal void DoBlock(BinaryWriter reloc, uint page, int start, int end)
        {
            //Console.WriteLine("rva = " + rva + "  page = " + page);
            if (Diag.DiagOn) Console.WriteLine("writing reloc block at " + reloc.BaseStream.Position);
            reloc.Write(rva + page);
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
            output.Write(tide);                 // Virtual size
            output.Write(rva);                  // Virtual address
            output.Write(size);                 // SizeOfRawData
            output.Write(offset);               // PointerToRawData
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