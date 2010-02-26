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
using System.Diagnostics;
using System.Collections;


namespace QUT.PERWAPI
{
    /**************************************************************************/
    // Class to Write PE File
    /**************************************************************************/

    internal class PEWriter : BinaryWriter
    {
        private Section text, sdata, rsrc = null;
        ArrayList data;  // Used for accumulating data for the .sdata Section.
        PEResourceDirectory unmanagedResourceRoot;
        BinaryWriter reloc = new BinaryWriter(new MemoryStream());
        uint dateStamp = 0, codeStart = 0;
        uint numSections = 2; // always have .text  and .reloc sections
        internal PEFileVersionInfo verInfo;
        //internal bool delaySign;
        uint entryPointOffset, entryPointPadding, imageSize, headerSize, headerPadding, entryPointToken = 0;
        uint relocOffset, relocRVA, relocSize, relocPadding, relocTide, hintNameTableOffset, resourcesSize = 0;
        uint metaDataOffset, runtimeEngineOffset, initDataSize = 0, resourcesOffset, importTablePadding;
        uint importTableOffset, importLookupTableOffset, totalImportTableSize, entryPointReloc = 0; //, delaySignOffset;
        uint debugOffset = 0, debugSize = 0, debugRVA = 0;
        long debugBytesStartOffset = 0;
        MetaDataOut metaData;
        char[] runtimeEngine = FileImage.runtimeEngineName.ToCharArray(), hintNameTable;
        bool closeStream = true;
        int debugBytesSize = 25; // NOTE: I don't know that this should be 25 but the debug bytes size seems to be 25 plus the size of the PDB filename. AKB 06-01-2007
        internal PDBWriter pdbWriter;

        /*-------------------- Constructors ---------------------------------*/

        internal PEWriter(PEFileVersionInfo verInfo, string fileName, MetaDataOut md, bool writePDB)
            : base(new FileStream(fileName, FileMode.Create))
        {
            InitPEWriter(verInfo, md, writePDB, fileName);
            TimeSpan tmp = System.IO.File.GetCreationTime(fileName).Subtract(FileImage.origin);
            dateStamp = Convert.ToUInt32(tmp.TotalSeconds);
        }

        internal PEWriter(PEFileVersionInfo verInfo, Stream str, MetaDataOut md)
            : base(str)
        {
            // NOTE: Can not write a PDB file if using a stream.
            InitPEWriter(verInfo, md, false, null);
            TimeSpan tmp = DateTime.Now.Subtract(FileImage.origin);
            dateStamp = Convert.ToUInt32(tmp.TotalSeconds);
            closeStream = false;
        }



        /*----------------------------- Writing -----------------------------------------*/

        private void InitPEWriter(PEFileVersionInfo verInfo, MetaDataOut md, bool writePDB, string fileName)
        {
            this.verInfo = verInfo;
            if (!verInfo.fromExisting)
                verInfo.lMajor = MetaData.LMajors[(int)verInfo.netVersion];
            if (verInfo.isDLL)
            {
                hintNameTable = FileImage.dllHintNameTable.ToCharArray();
                if (!verInfo.fromExisting) verInfo.characteristics = FileImage.dllCharacteristics;
            }
            else
            {
                hintNameTable = FileImage.exeHintNameTable.ToCharArray();
                if (!verInfo.fromExisting) verInfo.characteristics = FileImage.exeCharacteristics;
            }
            text = new Section(FileImage.textName, 0x60000020);     // IMAGE_SCN_CNT  CODE, EXECUTE, READ
            metaData = md;
            metaData.InitMetaDataOut(this);

            // Check if we should include a PDB file
            if (writePDB)
            {

                // Work out the PDB filename from the PE files filename
                if ((fileName == null) || (fileName == "")) fileName = "default";

                // Setup the PDB Writer object
                pdbWriter = new PDBWriter(fileName);

                // Set the amount of space required for the debug information
                debugBytesSize += pdbWriter.PDBFilename.Length;

            }

        }

        private uint GetNextSectStart(uint rva, uint tide)
        {
            if (tide < FileImage.SectionAlignment) return rva + FileImage.SectionAlignment;
            return rva + ((tide / FileImage.SectionAlignment) + 1) * FileImage.SectionAlignment;
        }

        private void BuildTextSection()
        {
            // .text layout
            //    IAT (single entry 8 bytes for pure CIL)
            //    CLIHeader (72 bytes)
            //    CIL instructions for all methods (variable size)
            //    Strong Name Signature
            //    MetaData 
            //    ManagedResources
            //    ImportTable (40 bytes)
            //    ImportLookupTable(8 bytes) (same as IAT for standard CIL files)
            //    Hint/Name Tables with entry "_CorExeMain" for .exe file and "_CorDllMain" for .dll (14 bytes)
            //    ASCII string "mscoree.dll" referenced in ImportTable (+ padding = 16 bytes)
            //    Entry Point  (0xFF25 followed by 4 bytes 0x400000 + RVA of .text)
            codeStart = FileImage.IATSize + FileImage.CLIHeaderSize;
            if (Diag.DiagOn) Console.WriteLine("Code starts at " + Hex.Int(codeStart));
            metaData.BuildMetaData();
            // strongNameSig = metaData.GetStrongNameSig();
            metaDataOffset = FileImage.IATSize + FileImage.CLIHeaderSize + metaData.CodeSize();
            if (pdbWriter != null)
            {
                debugSize = 0x1C; // or size of debugBytes??
                debugOffset = metaDataOffset;
                metaDataOffset += (uint)debugBytesSize + debugSize + NumToAlign((uint)debugBytesSize, 4);
            }
            resourcesOffset = metaDataOffset + metaData.Size();
            resourcesSize = metaData.GetResourcesSize();
            importTableOffset = resourcesOffset + resourcesSize;
            importTablePadding = NumToAlign(importTableOffset, 16);
            importTableOffset += importTablePadding;
            importLookupTableOffset = importTableOffset + FileImage.ImportTableSize;
            hintNameTableOffset = importLookupTableOffset + FileImage.IATSize;
            runtimeEngineOffset = hintNameTableOffset + (uint)hintNameTable.Length;
            entryPointOffset = runtimeEngineOffset + (uint)runtimeEngine.Length;
            totalImportTableSize = entryPointOffset - importTableOffset;
            if (Diag.DiagOn)
            {
                Console.WriteLine("total import table size = " + totalImportTableSize);
                Console.WriteLine("entrypoint offset = " + Hex.Int(entryPointOffset));
            }
            entryPointPadding = NumToAlign(entryPointOffset, 4) + 2;
            entryPointOffset += entryPointPadding;
            entryPointReloc = entryPointOffset + 2;
            text.IncTide(entryPointOffset + 6);
            // The following lines may have some benefit for speed,
            // but can increase the PE file size by up to 10k in
            // some circumstances.  Can be commented out safely.
            if (text.Tide() > 8 * FileImage.maxFileAlign)
                verInfo.fileAlign = FileImage.maxFileAlign;
            else if (text.Tide() > 2 * FileImage.maxFileAlign)
                verInfo.fileAlign = FileImage.midFileAlign;

            text.SetSize(NumToAlign(text.Tide(), verInfo.fileAlign));
            if (Diag.DiagOn)
            {
                Console.WriteLine("text size = " + text.Size() + " text tide = " + text.Tide() + " text padding = " + text.Padding());
                Console.WriteLine("metaDataOffset = " + Hex.Int(metaDataOffset));
                Console.WriteLine("importTableOffset = " + Hex.Int(importTableOffset));
                Console.WriteLine("importLookupTableOffset = " + Hex.Int(importLookupTableOffset));
                Console.WriteLine("hintNameTableOffset = " + Hex.Int(hintNameTableOffset));
                Console.WriteLine("runtimeEngineOffset = " + Hex.Int(runtimeEngineOffset));
                Console.WriteLine("entryPointOffset = " + Hex.Int(entryPointOffset));
                Console.WriteLine("entryPointPadding = " + Hex.Int(entryPointPadding));
            }
        }

        internal void BuildRelocSection()
        {
            // do entry point reloc
            uint relocPage = entryPointReloc / Section.relocPageSize;
            uint pageOff = relocPage * Section.relocPageSize;
            reloc.Write(text.RVA() + pageOff);
            reloc.Write(12);
            uint fixUpOff = entryPointReloc - pageOff;
            reloc.Write((ushort)((0x3 << 12) | fixUpOff));
            reloc.Write((ushort)0);
            // text.DoRelocs(reloc);
            if (sdata != null) sdata.DoRelocs(reloc);
            if (rsrc != null) rsrc.DoRelocs(reloc);
            relocTide = (uint)reloc.Seek(0, SeekOrigin.Current);
            //reloc.Write((uint)0);
            if (Diag.DiagOn) Console.WriteLine("relocTide = " + relocTide);
            relocPadding = NumToAlign(relocTide, verInfo.fileAlign);
            relocSize = relocTide + relocPadding;
            imageSize = relocRVA + FileImage.SectionAlignment;
            initDataSize += relocSize;
        }

        private void CalcOffsets()
        {
            if (sdata != null) numSections++;
            if (rsrc != null) numSections++;
            headerSize = FileImage.fileHeaderSize + (numSections * FileImage.sectionHeaderSize);
            headerPadding = NumToAlign(headerSize, verInfo.fileAlign);
            headerSize += headerPadding;
            uint offset = headerSize;
            uint rva = FileImage.SectionAlignment;
            text.SetOffset(offset);
            text.SetRVA(rva);
            if (pdbWriter != null) debugRVA = rva + debugOffset;
            offset += text.Size();
            rva = GetNextSectStart(rva, text.Tide());
            // Console.WriteLine("headerSize = " + headerSize);
            // Console.WriteLine("headerPadding = " + headerPadding);
            // Console.WriteLine("textOffset = " + Hex.Int(text.Offset()));
            if (sdata != null)
            {
                sdata.SetOffset(offset);
                sdata.SetRVA(rva);
                for (int i = 0; i < data.Count; i++)
                {
                    DataConstant cVal = (DataConstant)data[i];
                    cVal.DataOffset = sdata.Tide();
                    sdata.IncTide(cVal.GetSize());
                }
                sdata.SetSize(NumToAlign(sdata.Tide(), verInfo.fileAlign));
                offset += sdata.Size();
                rva = GetNextSectStart(rva, sdata.Tide());
                initDataSize += sdata.Size();
            }
            if (rsrc != null)
            {
                //Console.WriteLine("Resource section is not null");
                rsrc.SetSize(NumToAlign(rsrc.Tide(), verInfo.fileAlign));
                rsrc.SetOffset(offset);
                rsrc.SetRVA(rva);
                offset += rsrc.Size();
                rva = GetNextSectStart(rva, rsrc.Tide());
                initDataSize += rsrc.Size();
            }
            relocOffset = offset;
            relocRVA = rva;
        }

        internal void MakeFile(PEFileVersionInfo verInfo)
        {
            this.verInfo = verInfo;
            if (this.verInfo.isDLL) hintNameTable = FileImage.dllHintNameTable.ToCharArray();
            else hintNameTable = FileImage.exeHintNameTable.ToCharArray();

            BuildTextSection();
            CalcOffsets();
            BuildRelocSection();
            // now write it out
            WriteHeader();
            WriteSections();
            Flush();
            if (closeStream) Close();
            if (pdbWriter != null)
            {

                // Write the PDB file
                pdbWriter.WritePDBFile();

                // Check to make sure the DebugInfo is the length we expected.
                if (pdbWriter.DebugInfo.Length != debugBytesSize)
                    throw new Exception("DebugInfo for the new PDB file is incompatible with the PE file.  This is most likely an internal error.  Please consult your vendor if you continue to have this problem.");

                // Write the debug info to the PE file
                using (FileStream fs = new FileStream(pdbWriter.PEFilename, FileMode.Open, FileAccess.ReadWrite))
                {
                    using (BinaryWriter bw = new BinaryWriter(fs))
                    {
                        // Get to the DebugInfo section
                        bw.Seek((int)debugBytesStartOffset, SeekOrigin.Begin);
                        bw.Write(pdbWriter.DebugInfo, 0, pdbWriter.DebugInfo.Length);
                    }
                }

            }
        }

        private void WriteHeader()
        {
            Write(FileImage.DOSHeader);
            // Console.WriteLine("Writing PEHeader at offset " + Seek(0,SeekOrigin.Current));
            WritePEHeader();
            // Console.WriteLine("Writing text section header at offset " + Hex.Long(Seek(0,SeekOrigin.Current)));
            text.WriteHeader(this, relocRVA);
            if (sdata != null) sdata.WriteHeader(this, relocRVA);
            if (rsrc != null) rsrc.WriteHeader(this, relocRVA);
            // Console.WriteLine("Writing reloc section header at offset " + Seek(0,SeekOrigin.Current));
            WriteRelocSectionHeader();
            // Console.WriteLine("Writing padding at offset " + Seek(0,SeekOrigin.Current));
            WriteZeros(headerPadding);
        }

        private void WriteSections()
        {
            // Console.WriteLine("Writing text section at offset " + Seek(0,SeekOrigin.Current));
            WriteTextSection();
            if (sdata != null) WriteSDataSection();
            if (rsrc != null) WriteRsrcSection();
            WriteRelocSection();
        }

        private void WriteIAT()
        {
            Write(text.RVA() + hintNameTableOffset);
            Write(0);
        }

        private void WriteImportTables()
        {
            // Import Table
            WriteZeros(importTablePadding);
            //Console.WriteLine("Writing import tables at offset " + Hex.Long(Seek(0,SeekOrigin.Current)));
            //Console.WriteLine("Should be at offset " + Hex.Long(importTableOffset + text.Offset()));
            Write(importLookupTableOffset + text.RVA());
            WriteZeros(8);
            Write(runtimeEngineOffset + text.RVA());
            Write(text.RVA());    // IAT is at the beginning of the text section
            WriteZeros(20);
            // Import Lookup Table
            WriteIAT();                // lookup table and IAT are the same
            // Hint/Name Table
            // Console.WriteLine("Writing hintname table at " + Hex.Long(Seek(0,SeekOrigin.Current)));
            Write(hintNameTable);
            Write(FileImage.runtimeEngineName.ToCharArray());
        }

        private void WriteTextSection()
        {
            WriteIAT();
            WriteCLIHeader();
            if (Diag.DiagOn)
                Console.WriteLine("Writing code at " + Hex.Long(Seek(0, SeekOrigin.Current)));
            metaData.WriteByteCodes(this);
            if (Diag.DiagOn)
                Console.WriteLine("Finished writing code at " + Hex.Long(Seek(0, SeekOrigin.Current)));

            WriteDebugInfo();
            metaData.WriteMetaData(this);
            metaData.WriteResources(this);
            WriteImportTables();
            WriteZeros(entryPointPadding);
            Write((ushort)0x25FF);
            Write(FileImage.ImageBase + text.RVA());
            WriteZeros(text.Padding());
        }

        /// <summary>
        /// Write out the debug infro required for PDB files to the PE file.
        /// </summary>
        private void WriteDebugInfo()
        {
            if (pdbWriter != null)
            {                  // WINNT.h IMAGE_DEBUG_DIRECTORY
                WriteZeros(4);                        // Characteristics
                Write(dateStamp);                     // Date stamp
                WriteZeros(4);                        // Major Version, Minor Version
                Write(2);                             // Type  (Code View???)
                Write(debugBytesSize);             // Size of Data
                WriteZeros(4);                        // Address of Raw Data
                Write(text.Offset() + debugOffset + debugSize);     // Pointer to Raw Data

                if (Diag.DiagOn)
                    Debug.WriteLine("Debug Bytes Offset: " + BaseStream.Length.ToString());

                // Remember where the debug bytes need to be written to
                debugBytesStartOffset = BaseStream.Length;

                // For now don't write the real debug bytes. 
                // Just fill the space so we can come and write them later.
                // Write(debugBytes);
                WriteZeros((uint)debugBytesSize);

                WriteZeros(NumToAlign((uint)debugBytesSize, 4));
            }
        }


        private void WriteCLIHeader()
        {
            Write(FileImage.CLIHeaderSize);       // Cb
            Write(verInfo.cliMajVer);            // Major runtime version
            Write(verInfo.cliMinVer);            // Minor runtime version
            Write(text.RVA() + metaDataOffset);
            if (Diag.DiagOn) Console.WriteLine("MetaDataOffset = " + metaDataOffset);
            Write(metaData.Size());
            Write((uint)verInfo.corFlags);
            Write(entryPointToken);
            if (resourcesSize > 0)
            {  // managed resources
                Write(text.RVA() + resourcesOffset);
                Write(resourcesSize);
            }
            else
            {
                WriteZeros(8);
            }
            WriteZeros(8);                     // Strong Name stuff here!! NYI
            WriteZeros(8);                     // CodeManagerTable
            WriteZeros(8);                     // VTableFixups NYI
            WriteZeros(16);                    // ExportAddressTableJumps, ManagedNativeHeader
        }

        private void WriteSDataSection()
        {
            long pos = BaseStream.Position;
            for (int i = 0; i < data.Count; i++)
            {
                ((DataConstant)data[i]).Write(this);
            }
            pos = BaseStream.Position - pos;
            WriteZeros(NumToAlign((uint)pos, verInfo.fileAlign));
        }

        private void WriteRsrcSection()
        {
            //Console.WriteLine("Trying to write rsrc section !!!");
            long pos = BaseStream.Position;
            this.unmanagedResourceRoot.Write(this, rsrc.RVA());
            pos = BaseStream.Position - pos;
            WriteZeros(NumToAlign((uint)pos, verInfo.fileAlign));
        }

        private void WriteRelocSection()
        {
            // Console.WriteLine("Writing reloc section at " + Seek(0,SeekOrigin.Current) + " = " + relocOffset);
            MemoryStream str = (MemoryStream)reloc.BaseStream;
            Write(str.ToArray());
            WriteZeros(NumToAlign((uint)str.Position, verInfo.fileAlign));
        }

        internal void SetEntryPoint(uint entryPoint)
        {
            entryPointToken = entryPoint;
        }

        internal void AddInitData(DataConstant cVal)
        {
            if (sdata == null)
            {
                sdata = new Section(FileImage.sdataName, 0xC0000040);   // IMAGE_SCN_CNT  INITIALIZED_DATA, READ, WRITE
                data = new ArrayList();
            }
            data.Add(cVal);
        }

        internal void AddUnmanagedResourceDirectory(PEResourceDirectory directory) {
          if (rsrc == null)
            rsrc = new Section(FileImage.rsrcName, 0x40000040);
          rsrc.IncTide(directory.Size());
          unmanagedResourceRoot = directory;
        }

        internal void WriteZeros(uint numZeros)
        {
            for (int i = 0; i < numZeros; i++)
            {
                Write((byte)0);
            }
        }

        internal void WritePEHeader()
        {
            Write((ushort)0x014C);  // Machine - always 0x14C for Managed PE Files (allow others??)
            Write((ushort)numSections);
            Write(dateStamp);
            WriteZeros(8); // Pointer to Symbol Table and Number of Symbols (always zero for ECMA CLI files)
            Write((ushort)0x00E0);  // Size of Optional Header
            Write(verInfo.characteristics);
            // PE Optional Header
            Write((ushort)0x010B);   // Magic
            Write(verInfo.lMajor);        // LMajor pure-IL = 6   C++ = 7
            Write(verInfo.lMinor);
            Write(text.Size());
            Write(initDataSize);
            Write(0);                // Check other sections here!!
            Write(text.RVA() + entryPointOffset);
            Write(text.RVA());
            uint dataBase = 0;
            if (sdata != null) dataBase = sdata.RVA();
            else if (rsrc != null) dataBase = rsrc.RVA();
            else dataBase = relocRVA;
            Write(dataBase);
            Write(FileImage.ImageBase);
            Write(FileImage.SectionAlignment);
            Write(verInfo.fileAlign);
            Write(verInfo.osMajor);
            Write(verInfo.osMinor);
            Write(verInfo.userMajor);
            Write(verInfo.userMinor);
            Write(verInfo.subSysMajor);     // OS Major
            Write(verInfo.subSysMinor);
            WriteZeros(4);           // Reserved
            Write(imageSize);
            Write(headerSize);
            Write((int)0);           // File Checksum
            Write((ushort)verInfo.subSystem);
            Write(verInfo.DLLFlags);
            Write(FileImage.StackReserveSize);
            Write(FileImage.StackCommitSize);
            Write(FileImage.HeapReserveSize);
            Write(FileImage.HeapCommitSize);
            Write(FileImage.LoaderFlags);
            Write(FileImage.NumDataDirectories);  // Data Directories
            WriteZeros(8);                        // Export Table
            Write(importTableOffset + text.RVA());
            Write(totalImportTableSize);

            if (rsrc != null) {
              Write(rsrc.RVA());
              Write(rsrc.Tide()); // Tide() is loadedSize, Size() is sizeOnDisk.
            }
            else
              WriteZeros(8);
            WriteZeros(16);            // Exception and Certificate Tables
            Write(relocRVA);
            Write(relocTide);
            Write(debugRVA);
            Write(debugSize);
            WriteZeros(40);            // Copyright, Global Ptr, TLS, Load Config and Bound Import Tables
            Write(text.RVA());         // IATRVA - IAT is at start of .text Section
            Write(FileImage.IATSize);
            WriteZeros(8);             // Delay Import Descriptor
            Write(text.RVA() + FileImage.IATSize); // CLIHeader immediately follows IAT
            Write(FileImage.CLIHeaderSize);
            WriteZeros(8);             // Reserved
        }

        internal void WriteRelocSectionHeader()
        {
            Write(FileImage.relocName.ToCharArray());
            Write(relocTide);
            Write(relocRVA);
            Write(relocSize);
            Write(relocOffset);
            WriteZeros(12);
            Write(FileImage.relocFlags);
        }

        private void Align(MemoryStream str, int val)
        {
            if ((str.Position % val) != 0)
            {
                for (int i = val - (int)(str.Position % val); i > 0; i--)
                {
                    str.WriteByte(0);
                }
            }
        }

        private uint Align(uint val, uint alignVal)
        {
            if ((val % alignVal) != 0)
            {
                val += alignVal - (val % alignVal);
            }
            return val;
        }

        private uint NumToAlign(uint val, uint alignVal)
        {
            if ((val % alignVal) == 0) return 0;
            return alignVal - (val % alignVal);
        }

        internal void StringsIndex(uint ix)
        {
            if (metaData.largeStrings) Write(ix);
            else Write((ushort)ix);
        }

        internal void GUIDIndex(uint ix)
        {
            if (metaData.largeGUID) Write(ix);
            else Write((ushort)ix);
        }

        internal void USIndex(uint ix)
        {
            if (metaData.largeUS) Write(ix);
            else Write((ushort)ix);
        }

        internal void BlobIndex(uint ix)
        {
            if (metaData.largeBlob) Write(ix);
            else Write((ushort)ix);
        }

        internal void WriteIndex(MDTable tabIx, uint ix)
        {
            if (metaData.LargeIx(tabIx)) Write(ix);
            else Write((ushort)ix);
        }

        internal void WriteCodedIndex(CIx code, MetaDataElement elem)
        {
            metaData.WriteCodedIndex(code, elem, this);
        }

        internal void WriteCodeRVA(uint offs)
        {
            Write(text.RVA() + codeStart + offs);
        }

        internal void WriteDataRVA(uint offs)
        {
            Write(sdata.RVA() + offs);
        }

        internal void Write3Bytes(uint val)
        {
            byte b3 = (byte)((val & FileImage.iByteMask[2]) >> 16);
            byte b2 = (byte)((val & FileImage.iByteMask[1]) >> 8); ;
            byte b1 = (byte)(val & FileImage.iByteMask[0]);
            Write(b1);
            Write(b2);
            Write(b3);
        }

    }



}