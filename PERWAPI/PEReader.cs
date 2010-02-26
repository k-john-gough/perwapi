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
    // Class to Read PE Files
    /**************************************************************************/


    internal class PEReader : BinaryReader
    {

        private bool x64;
        internal static long blobStreamStartOffset = 0;
        private static readonly int cliIx = 14;
        private Section[] inputSections;
        int numSections = 0;
        uint[] DataDirectoryRVA = new uint[16];
        uint[] DataDirectorySize = new uint[16];
        uint[] streamOffsets, streamSizes;
        String[] streamNames;
        private TableRow[][] tables = new TableRow[MetaData.NumMetaDataTables][];
        uint[] tableLengths = new uint[MetaData.NumMetaDataTables];
        MetaData md = new MetaData();
        MetaDataStringStream userstring;
        MetaDataInStream blob, strings, guid;
        Sentinel sentinel = new Sentinel();
        Pinned pinned = new Pinned();
        //CorFlags corFlags;
        uint metaDataRVA = 0, metaDataSize = 0, flags = 0;
        uint entryPoint = 0, resourcesRVA = 0, resourcesSize = 0;
        uint strongNameRVA = 0, strongNameSize = 0, vFixupsRVA = 0, vFixupsSize = 0;
        //ushort dllFlags = 0, subSystem = 0;
        //uint fileAlign = 0;
        //char[] verString;
        bool refsOnly = false;
        long[] tableStarts;
        ResolutionScope thisScope;
        PEFileVersionInfo verInfo = new PEFileVersionInfo();
        internal Method currentMethodScope;
        internal Class currentClassScope;
        int genInstNestLevel = 0;
        internal bool skipBody = true;

        private PEReader(PEFile pefile, System.IO.FileStream file, bool refs, bool skipBody)
            :
            base(new MemoryStream(new BinaryReader(file).ReadBytes(System.Convert.ToInt32(file.Length))))
        {
            this.skipBody = skipBody;
            thisScope = pefile;
            refsOnly = refs;
            verInfo.fromExisting = true;
            try
            {
                ReadDOSHeader();
            }
            catch (PEFileException)
            {
                Console.WriteLine("Bad DOS header");
                return;
            }
            ReadFileHeader();
            ReadSectionHeaders();
            ReadCLIHeader();
            ReadMetaData();
            if (refsOnly)
                this.ReadMetaDataTableRefs();
            else
            {
                this.ReadMetaDataTables();
                pefile.metaDataTables = new MetaDataTables(tables);
                this.SaveUnmanagedResources();
            }
            file.Close();

            if (thisScope != null)
            {
                thisScope.buffer = this;
                if (pefile != null)
                {
                    pefile.versionInfo = verInfo;
                }
            }
            strings = null;
            userstring = null;
            blob = null;
            guid = null;
        }

        private static System.IO.FileStream GetFile(string filename)
        {
            if (Diag.DiagOn)
            {
                Console.WriteLine("Current directory is " + System.Environment.CurrentDirectory);
                Console.WriteLine("Looking for file " + filename);
            }
            if (System.IO.File.Exists(filename))
            {
                return System.IO.File.OpenRead(filename);
            }
            else
                throw (new System.IO.FileNotFoundException("File Not Found", filename));
        }

        public static PEFile ReadPEFile(string filename, bool skipBody)
        {
            System.IO.FileStream file = GetFile(filename);
            PEFile pefile = new PEFile(filename);
            PEReader reader = new PEReader(pefile, file, false, skipBody);
            return pefile;
        }



        internal static ReferenceScope GetExportedInterface(string filename)
        {
            System.IO.FileStream file = GetFile(filename);
            PEReader reader = new PEReader(null, file, true, true);
            return (ReferenceScope)reader.thisScope;
        }

        //internal ResolutionScope GetThisScope() { return thisScope; }

        internal string[] GetAssemblyRefNames()
        {
            string[] assemNames = new string[tableLengths[(int)MDTable.AssemblyRef]];
            for (int i = 0; i < assemNames.Length; i++)
            {
                assemNames[i] = ((AssemblyRef)tables[(int)MDTable.AssemblyRef][i]).Name();
            }
            return assemNames;
        }

        internal AssemblyRef[] GetAssemblyRefs()
        {
            AssemblyRef[] assemRefs = new AssemblyRef[tableLengths[(int)MDTable.AssemblyRef]];
            for (int i = 0; i < assemRefs.Length; i++)
            {
                assemRefs[i] = (AssemblyRef)tables[(int)MDTable.AssemblyRef][i];
            }
            return assemRefs;
        }
        /*----------------------------- Reading ----------------------------------------*/

        internal void InputError()
        {
            throw new PEFileException("Error in input");
        }

        internal void MetaDataError(string msg)
        {
            msg = "ERROR IN METADATA: " + msg;
            if (thisScope != null)
                msg = "MODULE " + thisScope.Name() + ": " + msg;
            throw new PEFileException(msg);
        }

        internal Section GetSection(uint rva)
        {
            for (int i = 0; i < inputSections.Length; i++)
            {
                if (inputSections[i].ContainsRVA(rva)) return inputSections[i];
            }
            return null;
        }

        internal uint GetOffset(uint rva)
        {
            for (int i = 0; i < inputSections.Length; i++)
            {
                if (inputSections[i].ContainsRVA(rva))
                    return inputSections[i].GetOffset(rva);
            }
            return 0;
        }

        internal void ReadZeros(int num)
        {
            for (int i = 0; i < num; i++)
            {
                byte next = ReadByte();
                if (next != 0) InputError();
            }
        }

        private void ReadDOSHeader()
        {
            for (int i = 0; i < FileImage.PESigOffset; i++)
            {
                if (FileImage.DOSHeader[i] != ReadByte())
                {
                    InputError();
                }
            }
            uint sigOffset = ReadUInt32();
            for (int i = FileImage.PESigOffset + 4; i < FileImage.DOSHeader.Length - 4; i++)
            {
                if (FileImage.DOSHeader[i] != ReadByte()) { InputError(); }
            }
            BaseStream.Seek(sigOffset, SeekOrigin.Begin);
            if ((char)ReadByte() != 'P') InputError();
            if ((char)ReadByte() != 'E') InputError();
            if (ReadByte() != 0) InputError();
            if (ReadByte() != 0) InputError();
        }

        private void ReadFileHeader()
        {
            // already read PE signature
            ushort machineid = ReadUInt16();
            if (machineid != FileImage.machine && machineid != FileImage.machinex64) InputError();
            numSections = ReadUInt16();
            uint TimeStamp = ReadUInt32();
            ReadZeros(8);     /* Pointer to Symbol Table, Number of Symbols */
            int optHeadSize = ReadUInt16();
            verInfo.characteristics = ReadUInt16();
            verInfo.isDLL = (verInfo.characteristics & FileImage.dllFlag) != 0;
            /* Now read PE Optional Header */
            /* Standard Fields */
            ushort magic = ReadUInt16();
            if (magic != FileImage.magic && magic != FileImage.magic64) InputError();
            x64 = magic == FileImage.magic64;
            verInfo.lMajor = ReadByte(); // != FileImage.lMajor) InputError();
            verInfo.lMinor = ReadByte(); // != FileImage.lMinor) InputError();
            uint codeSize = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("codeSize = " + Hex.Int(codeSize));
            uint initDataSize = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("initDataSize = " + Hex.Int(initDataSize));
            uint uninitDataSize = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("uninitDataSize = " + Hex.Int(uninitDataSize));
            uint entryPointRVA = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("entryPointRVA = " + Hex.Int(entryPointRVA));
            uint baseOfCode = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("baseOfCode = " + Hex.Int(baseOfCode));
            //uint baseOfData = ReadUInt32();
            if (!x64)
            {
                uint baseOfData = ReadUInt32();
                if (Diag.DiagOn) Console.WriteLine("baseOfData = " + Hex.Int(baseOfData));
            }
            /* NT-Specific Fields */
            ulong imageBase = x64 ? ReadUInt64() : ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("imageBase = " + Hex.Long(imageBase));
            uint sectionAlign = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("sectionAlign = " + Hex.Int(sectionAlign));
            verInfo.fileAlign = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("fileAlign = " + Hex.Int(verInfo.fileAlign));
            verInfo.osMajor = ReadUInt16();
            if (Diag.DiagOn) Console.WriteLine("osMajor = " + Hex.Int(verInfo.osMajor));
            //ReadZeros(6);         // osMinor, userMajor, userMinor
            verInfo.osMinor = ReadUInt16();
            verInfo.userMajor = ReadUInt16();
            verInfo.userMinor = ReadUInt16();
            verInfo.subSysMajor = ReadUInt16();
            if (Diag.DiagOn) Console.WriteLine("subsysMajor = " + Hex.Int(verInfo.subSysMajor));
            verInfo.subSysMinor = ReadUInt16();
            ReadZeros(4);         // Reserved
            uint imageSize = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("imageSize = " + Hex.Int(imageSize));
            uint headerSize = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("headerSize = " + Hex.Int(headerSize));
            uint checkSum = ReadUInt32();
            if (Diag.DiagOn) Console.WriteLine("checkSum = " + Hex.Int(checkSum));
            verInfo.subSystem = (SubSystem)ReadUInt16();
            if (Diag.DiagOn) Console.WriteLine("subSystem = " + Hex.Short((int)verInfo.subSystem));
            verInfo.DLLFlags = ReadUInt16();
            if (Diag.DiagOn) Console.WriteLine("DLLFlags = " + Hex.Short(verInfo.DLLFlags));
            ulong stackReserve = x64 ? ReadUInt64() : ReadUInt32(); //  if (ReadUInt32() != FileImage.StackReserveSize) InputError();
            ulong stackCommit = x64 ? ReadUInt64() : ReadUInt32(); // if (ReadUInt32() != FileImage.StackCommitSize) InputError();
            ulong heapReserve = x64 ? ReadUInt64() : ReadUInt32(); // if (ReadUInt32() != FileImage.HeapReserveSize) InputError();
            ulong heapCommit = x64 ? ReadUInt64() : ReadUInt32(); // if (ReadUInt32() != FileImage.HeapCommitSize) InputError();
            ReadUInt32(); // if (ReadUInt32() != 0) InputError(); // LoaderFlags
            uint numdict = ReadUInt32();
            if (numdict != FileImage.NumDataDirectories) InputError();
            /* Data Directories */
            DataDirectoryRVA = new uint[FileImage.NumDataDirectories];
            DataDirectorySize = new uint[FileImage.NumDataDirectories];
            // (Index 2 is resource table address and size)
            for (int i = 0; i < FileImage.NumDataDirectories; i++)
            {
                DataDirectoryRVA[i] = ReadUInt32();
                DataDirectorySize[i] = ReadUInt32();
            }
            if (Diag.DiagOn)
            {
                Console.WriteLine("RVA = " + Hex.Int(DataDirectoryRVA[1]) + "  Size = " + Hex.Int(DataDirectorySize[1]) + "  Import Table");
                Console.WriteLine("RVA = " + Hex.Int(DataDirectoryRVA[2]) + "  Size = " + Hex.Int(DataDirectorySize[2]) + "  Resource Table");
                Console.WriteLine("RVA = " + Hex.Int(DataDirectoryRVA[5]) + "  Size = " + Hex.Int(DataDirectorySize[5]) + "  Base Relocation Table");
                Console.WriteLine("RVA = " + Hex.Int(DataDirectoryRVA[12]) + "  Size = " + Hex.Int(DataDirectorySize[12]) + "  IAT");
                Console.WriteLine("RVA = " + Hex.Int(DataDirectoryRVA[14]) + "  Size = " + Hex.Int(DataDirectorySize[14]) + "  CLI Header");
            }
        }

        private void ReadSectionHeaders()
        {
            if (Diag.DiagOn) Console.WriteLine("Sections");
            inputSections = new Section[numSections];
            for (int i = 0; i < numSections; i++)
            {
                inputSections[i] = new Section(this);
            }
        }

        private void ReadCLIHeader()
        {
            BaseStream.Seek(GetOffset(DataDirectoryRVA[cliIx]), SeekOrigin.Begin);
            uint cliSize = ReadUInt32();
            verInfo.cliMajVer = ReadUInt16(); // check
            verInfo.cliMinVer = ReadUInt16(); // check
            metaDataRVA = ReadUInt32();
            metaDataSize = ReadUInt32();
            //Console.WriteLine("Meta Data at rva " + PEConsts.Hex(metaDataRVA) + "  size = " + PEConsts.Hex(metaDataSize));
            verInfo.corFlags = (CorFlags)ReadUInt32();
            entryPoint = ReadUInt32();
            resourcesRVA = ReadUInt32();
            resourcesSize = ReadUInt32();
            strongNameRVA = ReadUInt32();
            strongNameSize = ReadUInt32();
            ReadZeros(8); // CodeManagerTable
            vFixupsRVA = ReadUInt32();
            vFixupsSize = ReadUInt32();
            ReadZeros(16); // ExportAddressTableJumps/ManagedNativeHeader
        }

        private String ReadStreamName()
        {
            char[] strName = new char[9];
            strName[0] = (char)ReadByte();
            char ch = (char)ReadByte();
            int i = 1;
            while (ch != '\0') { strName[i++] = ch; ch = (char)ReadByte(); }
            strName[i++] = '\0';
            if (i % 4 != 0)
            {
                for (int j = 4 - i % 4; j > 0; j--) ReadByte();
            }
            return new String(strName, 0, i - 1);
        }

        private void ReadMetaData()
        {
            if (Diag.DiagOn) Console.WriteLine("MetaData at RVA = " + Hex.Int(metaDataRVA) + " and offset = " + Hex.Int(GetOffset(metaDataRVA)));
            BaseStream.Seek(GetOffset(metaDataRVA), SeekOrigin.Begin);
            uint sig = ReadUInt32();              // check
            verInfo.mdMajVer = ReadUInt16();           // check
            verInfo.mdMinVer = ReadUInt16();           // check
            ReadZeros(4);
            int verStrLen = ReadInt32();
            int end = -1;
            char[] verString = new char[verStrLen + 1];
            for (int i = 0; i < verStrLen; i++)
            {
                verString[i] = (char)ReadByte();
                if ((verString[i] == 0) && (end == -1)) end = i;
            }
            verString[verStrLen] = (char)0; // check
            if (end == -1) end = verStrLen;
            verInfo.netVerString = new string(verString, 0, end);
            verInfo.SetVersionFromString();

            // Beware of unknown netVerString values here ... Needs a better fix (kjg 31-Oct-2007)
            GenericParam.extraField = verInfo.netVersion < NetVersion.Whidbey41;
            if (Diag.DiagOn && GenericParam.extraField)
            {
                Console.WriteLine("Version = " + verInfo.netVerString + " has extra field for GenericParam");
            }
            int alignNum = 0;
            if ((verStrLen % 4) != 0) alignNum = 4 - (verStrLen % 4);
            ReadZeros(alignNum);
            flags = ReadUInt16(); // check
            int numStreams = ReadUInt16();
            streamOffsets = new uint[numStreams];
            streamSizes = new uint[numStreams];
            streamNames = new String[numStreams];
            if (Diag.DiagOn)
                Console.WriteLine("MetaData Streams");
            for (int i = 0; i < numStreams; i++)
            {
                streamOffsets[i] = ReadUInt32();
                streamSizes[i] = ReadUInt32();
                streamNames[i] = ReadStreamName();
                if (Diag.DiagOn)
                    Console.WriteLine("  " + streamNames[i] + "  Offset = " + Hex.Int(streamOffsets[i]) + "  Size = " + Hex.Int(streamSizes[i]));
            }
            uint tildeIx = 0;
            for (uint i = 0; i < numStreams; i++)
            {
                String nam = streamNames[i];
                if (MetaData.tildeName.CompareTo(nam) == 0) tildeIx = i;
                else
                {
                    uint streamoff = GetOffset(metaDataRVA + streamOffsets[i]);
                    if (Diag.DiagOn) Console.WriteLine("getting stream bytes at offset " + Hex.Int(streamoff));
                    BaseStream.Seek(GetOffset(metaDataRVA + streamOffsets[i]), SeekOrigin.Begin);
                    long streamStart = BaseStream.Position;
                    byte[] strBytes = ReadBytes((int)streamSizes[i]);
                    if (MetaData.stringsName.CompareTo(nam) == 0)
                    {
                        strings = new MetaDataInStream(strBytes);
                    }
                    else if (MetaData.userstringName.CompareTo(nam) == 0)
                    {
                        userstring = new MetaDataStringStream(strBytes);
                    }
                    else if (MetaData.blobName.CompareTo(nam) == 0)
                    {
                        blobStreamStartOffset = streamStart;
                        blob = new MetaDataInStream(strBytes);
                    }
                    else if (MetaData.guidName.CompareTo(nam) == 0)
                    {
                        guid = new MetaDataInStream(strBytes);
                    }
                    else if (nam.CompareTo("#-") == 0)
                    {
                        tildeIx = i;
                        //throw new Exception("Illegal uncompressed data stream #-");
                    }
                    else
                    {
                        Console.WriteLine("Unknown stream - " + nam);
                    }
                }
            }
            // go to beginning of tilde stream
            BaseStream.Seek(GetOffset(metaDataRVA + streamOffsets[tildeIx]), SeekOrigin.Begin);
            ReadTildeStreamStart();
        }

        private void SetUpTableInfo()
        {
            md.CalcElemSize();
            tableStarts = new long[MetaData.NumMetaDataTables];
            long currentPos = BaseStream.Position;
            for (int ix = 0; ix < MetaData.NumMetaDataTables; ix++)
            {
                tableStarts[ix] = currentPos;
                currentPos += tableLengths[ix] * md.elemSize[ix];
            }
        }

        private void ReadTildeStreamStart()
        {
            if (Diag.DiagOn) Console.WriteLine("Reading meta data tables at offset = " + Hex.Int((int)BaseStream.Position));
            // pre:  at beginning of tilde stream
            ReadZeros(4);  // reserved
            verInfo.tsMajVer = ReadByte();  // check
            verInfo.tsMinVer = ReadByte();  // check
            byte heapSizes = ReadByte();
            if (heapSizes != 0)
            {
                md.largeStrings = (heapSizes & 0x01) != 0;
                md.largeGUID = (heapSizes & 0x02) != 0;
                md.largeBlob = (heapSizes & 0x04) != 0;
            }
            if (Diag.DiagOn)
            {
                if (md.largeStrings) Console.WriteLine("LARGE strings index");
                if (md.largeGUID) Console.WriteLine("LARGE GUID index");
                if (md.largeBlob) Console.WriteLine("LARGE blob index");
            }
            int res = ReadByte(); // check if 1
            ulong valid = ReadUInt64();
            ulong sorted = this.ReadUInt64();
            if (Diag.DiagOn) Console.WriteLine("Valid = " + Hex.Long(valid));
            for (int i = 0; i < MetaData.NumMetaDataTables; i++)
            {
                if ((valid & FileImage.bitmasks[i]) != 0)
                {
                    tableLengths[i] = ReadUInt32();
                    tables[i] = new TableRow[tableLengths[i]];
                    md.largeIx[i] = tableLengths[i] > MetaData.maxSmlIxSize;
                    if (Diag.DiagOn)
                        Console.WriteLine("Table Ix " + Hex.Short(i) + " has length " + tableLengths[i]);
                }
                else tableLengths[i] = 0;
            }
            if (tableLengths[0] != 1) this.MetaDataError("Module table has more than one entry");
            for (int i = 0; i < MetaData.CIxTables.Length; i++)
            {
                for (int j = 0; j < MetaData.CIxTables[i].Length; j++)
                {
                    if (Diag.DiagOn) Console.WriteLine("CIxTables " + i + " " + j + " tableLength = " + tableLengths[MetaData.CIxTables[i][j]] + "  Max = " + MetaData.CIxMaxMap[i]);
                    md.lgeCIx[i] = md.lgeCIx[i] ||
                        (tableLengths[MetaData.CIxTables[i][j]] > MetaData.CIxMaxMap[i]);
                }
                if (Diag.DiagOn) if (md.lgeCIx[i]) Console.WriteLine("LARGE CIx " + i);
            }
        }

        private void SetThisScope()
        {
            if (refsOnly)
                thisScope = Module.ReadModuleRef(this);
            else
                ((PEFile)thisScope).Read(this);
            tables[(int)MDTable.Module][0] = thisScope;
            if (tableLengths[(int)MDTable.Assembly] > 0)
            {
                SetElementPosition(MDTable.Assembly, 1);
                if (refsOnly)
                {
                    ModuleRef thisMod = (ModuleRef)thisScope;
                    thisScope = Assembly.ReadAssemblyRef(this);
                    //if ((thisMod != null) && (thisMod.ismscorlib) && (thisScope != null)) {
                    //  ((AssemblyRef)thisScope).CopyVersionInfoToMSCorLib();
                    //  thisScope = MSCorLib.mscorlib;
                    //}
                    tables[(int)MDTable.Assembly][0] = thisScope;
                }
                else
                {
                    Assembly.Read(this, tables[(int)MDTable.Assembly], (PEFile)thisScope);
                    ((PEFile)thisScope).SetThisAssembly((Assembly)tables[(int)MDTable.Assembly][0]);
                }
            }
        }

        /// <summary>
        /// Read the Module metadata for this PE file.
        /// If reading refs only, then thisModule is the ModuleRef 
        /// If reading defs then pefile is the Module 
        /// </summary>
        /*    private void GetThisPEFileScope() {
              if (refsOnly) {
                thisModuleRef = Module.ReadModuleRef(this);
              } else { 
                pefile.Read(this);
                tables[(int)MDTable.Module][0] = pefile;
              }
            }

            private AssemblyRef GetThisAssembly(bool atPos) {
              if (tableLengths[(int)MDTable.Assembly] == 0) return null;
              if (!atPos)
                BaseStream.Position = tableStarts[(int)MDTable.Assembly];
              if (refsOnly) 
                tables[(int)MDTable.Assembly][0] = Assembly.ReadAssemblyRef(this);
              else 
                Assembly.Read(this,tables[(int)MDTable.Assembly],pefile);
              return (AssemblyRef)tables[(int)MDTable.Assembly][0];
            }
        */
        /*
        private ReferenceScope ReadRefsOnDemand() {
          ModuleRef thisModule;
          SetUpTableInfo();
          ResolutionScope mod;

          AssemblyRef thisAssembly = GetThisAssemblyRef();
          SetElementPosition(MDTable.Module,0);
          ReadZeros(2);
          name = buff.GetString();
          mvid = buff.GetGUID();
          ModuleRef thisMod = ModuleRef.GetModuleRef(name);
          if (thisMod == null) {
            thisMod = new ModuleRef(name);
            Module.AddToList(thisMod);
          } else {
          }
          if (thisModule == null) {
            thisModule = new ModuleRef(name);
            thisModule.readAsDef = true;
            if (mod != null) ((Module)mod).refOf = thisModule;
            else Module.AddToList(thisModule);
          } else {
            if (thisModule.readAsDef) return thisModule;
            return Merge(thisModule);
          }
          ReferenceScope thisScope = thisAssembly;
          if (thisScope == null) thisScope = thisModule;
          ClassRef defClass = ReadDefaultClass();
          thisScope.SetDefaultClass(defClass);
          ClassDef.GetClassRefNames(this,thisScope);
      
          return null;
        }
        */

        private void ReadMetaDataTableRefs()
        {
            SetUpTableInfo();
            SetThisScope();
            // ReadAssemblyRefs
            SetElementPosition(MDTable.AssemblyRef, 1);
            if (tableLengths[(int)MDTable.AssemblyRef] > 0)
                AssemblyRef.Read(this, tables[(int)MDTable.AssemblyRef]);
            // Read File Table (for ModuleRefs)
            //SetElementPosition(MDTable.File,1);
            if (tableLengths[(int)MDTable.File] > 0)
                FileRef.Read(this, tables[(int)MDTable.File]);
            // Read Exported Classes
            //SetElementPosition(MDTable.ExportedType,1);
            if (tableLengths[(int)MDTable.ExportedType] > 0)
                ExternClass.GetClassRefs(this, tables[(int)MDTable.ExportedType]);
            // Read ModuleRefs
            if (tableLengths[(int)MDTable.ModuleRef] > 0)
            {
                BaseStream.Position = tableStarts[(int)MDTable.ModuleRef];
                ModuleRef.Read(this, tables[(int)MDTable.ModuleRef], true);
            }
            uint[] parIxs = new uint[tableLengths[(int)MDTable.TypeDef]];
            BaseStream.Position = tableStarts[(int)MDTable.NestedClass];
            MapElem.ReadNestedClassInfo(this, tableLengths[(int)MDTable.NestedClass], parIxs);
            BaseStream.Position = tableStarts[(int)MDTable.TypeRef];
            // Read ClassRefs
            if (tableLengths[(int)MDTable.TypeRef] > 0)
                ClassRef.Read(this, tables[(int)MDTable.TypeRef], true);
            // Read ClassDefs and fields and methods
            ClassDef.GetClassRefs(this, tables[(int)MDTable.TypeDef], (ReferenceScope)thisScope, parIxs);
            for (int i = 0; i < tableLengths[(int)MDTable.ExportedType]; i++)
            {
                ((ClassRef)tables[(int)MDTable.ExportedType][i]).ResolveParent(this, true);
            }
        }

        internal void SetElementPosition(MDTable tabIx, uint ix)
        {
            BaseStream.Position = tableStarts[(int)tabIx] + (md.elemSize[(int)tabIx] * (ix - 1));
        }

        internal void ReadMethodImpls(ClassDef theClass, uint classIx)
        {
            SetElementPosition(MDTable.InterfaceImpl, 0);
            for (int i = 0; (i < tableLengths[(int)MDTable.MethodImpl]); i++)
            {
                uint clIx = GetIndex(MDTable.TypeDef);
                uint bodIx = GetCodedIndex(CIx.MethodDefOrRef);
                uint declIx = GetCodedIndex(CIx.MethodDefOrRef);
                if (clIx == classIx)
                {
                    MethodImpl mImpl = new MethodImpl(this, theClass, bodIx, declIx);
                    theClass.AddMethodImpl(mImpl);
                    tables[(int)MDTable.MethodImpl][i] = mImpl;
                }
            }
        }

        internal void InsertInTable(MDTable tabIx, uint ix, MetaDataElement elem)
        {
            tables[(int)tabIx][ix - 1] = elem;
        }

        private void CheckForRefMerges()
        {
            if (tableLengths[(int)MDTable.TypeRef] > 0)
            {
                for (int i = 0; i < tableLengths[(int)MDTable.TypeRef]; i++)
                {
                    ((ClassRef)tables[(int)MDTable.TypeRef][i]).ResolveParent(this, false);
                }
            }
            if (tableLengths[(int)MDTable.MemberRef] > 0)
            {
                for (int i = 0; i < tableLengths[(int)MDTable.MemberRef]; i++)
                {
                    Member memb = (Member)tables[(int)MDTable.MemberRef][i];
                    tables[(int)MDTable.MemberRef][i] = memb.ResolveParent(this);
                }
            }
        }

        internal void ReplaceSig(Signature sig, Type sigType)
        {
            tables[(int)MDTable.StandAloneSig][sig.Row - 1] = sigType;
        }

        internal void GetGenericParams(MethodDef meth)
        {
            if (tables[(int)MDTable.GenericParam] != null)
            {
                for (int j = 0; j < tables[(int)MDTable.GenericParam].Length; j++)
                {
                    ((GenericParam)tables[(int)MDTable.GenericParam][j]).CheckParent(meth, this);
                }
            }
        }

        private void ReadMetaDataTables()
        {
            ((PEFile)thisScope).Read(this);
            tables[(int)MDTable.Module][0] = thisScope;
            for (int ix = 1; ix < MetaData.NumMetaDataTables; ix++)
            {
                if (tableLengths[ix] > 0)
                {
                    switch (ix)
                    {
                        case ((int)MDTable.Assembly):
                            Assembly.Read(this, tables[ix], (PEFile)thisScope);
                            break;
                        case ((int)MDTable.AssemblyOS):
                        case ((int)MDTable.AssemblyProcessor):
                        case ((int)MDTable.AssemblyRefOS):
                        case ((int)MDTable.AssemblyRefProcessor):
                            // ignore
                            Console.WriteLine("Got uncompressed table " + (MDTable)ix);
                            BaseStream.Seek(tableLengths[ix] * md.elemSize[ix], SeekOrigin.Current);
                            break;
                        case ((int)MDTable.AssemblyRef):
                            AssemblyRef.Read(this, tables[ix]); break;
                        //case 0x25 : AssemblyRefOS.Read(this,tables[ix]); break;
                        //case 0x24 : AssemblyRefProcessor.Read(this,tables[ix]); break;
                        case ((int)MDTable.ClassLayout):
                            ClassLayout.Read(this, tables[ix]); break;
                        case ((int)MDTable.Constant):
                            ConstantElem.Read(this, tables[ix]); break;
                        case ((int)MDTable.CustomAttribute):
                            CustomAttribute.Read(this, tables[ix]); break;
                        case ((int)MDTable.DeclSecurity):
                            DeclSecurity.Read(this, tables[ix]); break;
                        case ((int)MDTable.Event):
                            Event.Read(this, tables[ix]); break;
                        case ((int)MDTable.EventMap):
                            MapElem.Read(this, tables[ix], MDTable.EventMap); break;
                        case ((int)MDTable.ExportedType):
                            ExternClass.Read(this, tables[ix]); break;
                        case ((int)MDTable.Field):
                            FieldDef.Read(this, tables[ix]); break;
                        case ((int)MDTable.FieldLayout):
                            FieldLayout.Read(this, tables[ix]); break;
                        case ((int)MDTable.FieldMarshal):
                            FieldMarshal.Read(this, tables[ix]); break;
                        case ((int)MDTable.FieldRVA):
                            FieldRVA.Read(this, tables[ix]); break;
                        case ((int)MDTable.File):
                            FileRef.Read(this, tables[ix]); break;
                        case ((int)MDTable.GenericParam):
                            GenericParam.Read(this, tables[ix]); break;
                        case ((int)MDTable.GenericParamConstraint):
                            GenericParamConstraint.Read(this, tables[ix]); break;
                        case ((int)MDTable.ImplMap):
                            ImplMap.Read(this, tables[ix]); break;
                        case ((int)MDTable.InterfaceImpl):
                            InterfaceImpl.Read(this, tables[ix]); break;
                        case ((int)MDTable.ManifestResource):
                            ManifestResource.Read(this, tables[ix]); break;
                        case ((int)MDTable.MemberRef):
                            Member.ReadMember(this, tables[ix]); break;
                        case ((int)MDTable.Method):
                            MethodDef.Read(this, tables[ix]); break;
                        case ((int)MDTable.MethodImpl):
                            MethodImpl.Read(this, tables[ix]); break;
                        case ((int)MDTable.MethodSemantics):
                            MethodSemantics.Read(this, tables[ix]); break;
                        case ((int)MDTable.MethodSpec):
                            MethodSpec.Read(this, tables[ix]); break;
                        case ((int)MDTable.ModuleRef):
                            ModuleRef.Read(this, tables[ix], false); break;
                        case ((int)MDTable.NestedClass):
                            MapElem.Read(this, tables[ix], MDTable.NestedClass);
                            tables[ix] = null;
                            break;
                        case ((int)MDTable.Param):
                            Param.Read(this, tables[ix]); break;
                        case ((int)MDTable.Property):
                            Property.Read(this, tables[ix]); break;
                        case ((int)MDTable.PropertyMap):
                            MapElem.Read(this, tables[ix], MDTable.PropertyMap); break;
                        case ((int)MDTable.StandAloneSig):
                            Signature.Read(this, tables[ix]); break;
                        case ((int)MDTable.TypeDef):
                            ClassDef.Read(this, tables[ix], ((PEFile)thisScope).isMSCorLib());
                            break;
                        case ((int)MDTable.TypeRef):
                            ClassRef.Read(this, tables[ix], false); break;
                        case ((int)MDTable.TypeSpec):
                            TypeSpec.Read(this, tables[ix]); break;
                        default: throw (new PEFileException("Unknown MetaData Table Type"));
                    }
                }
            }
            CheckForRefMerges();
            for (int ix = 0; ix < MetaData.NumMetaDataTables; ix++)
            {
                if ((tables[ix] != null) && (ix != (int)MDTable.TypeSpec) &&
                    (ix != (int)MDTable.MethodSpec))
                {  // resolve type/method specs when referenced
                    for (int j = 0; j < tables[ix].Length; j++)
                    {
                        //tables[ix][j].Row = (uint)j+1;
                        // KJG fix 2005:02:23
                        //   Everett ILASM leaves gaps in table[10][x] ... 
                        //   so protect with a null test.
                        //
                        // ((MetaDataElement)tables[ix][j]).Resolve(this); // old line ...
                        //
                        if (tables[ix][j] != null)
                        {
                            ((MetaDataElement)tables[ix][j]).Resolve(this);
                        }
                    }
                }
            }
            if (tableLengths[(int)MDTable.Assembly] > 0)
                ((PEFile)thisScope).SetThisAssembly((Assembly)tables[(int)MDTable.Assembly][0]);
            ((PEFile)thisScope).SetDefaultClass((ClassDef)tables[(int)MDTable.TypeDef][0]);
            for (int j = 1; j < tables[(int)MDTable.TypeDef].Length; j++)
            {
                ((PEFile)thisScope).AddToClassList((ClassDef)tables[(int)MDTable.TypeDef][j]);
            }
            if (tableLengths[(int)MDTable.ManifestResource] > 0)
            {
                for (int j = 0; j < tables[(int)MDTable.ManifestResource].Length; j++)
                {
                    ((PEFile)thisScope).AddToResourceList((ManifestResource)tables[(int)MDTable.ManifestResource][j]);
                }
            }
            // We must protect the following code, since it seems possible
            // that "entryPoint" is a random value if isDLL is false, 
            // leading to an index out of bounds error in some cases.
            if (!verInfo.isDLL && entryPoint != 0)
            {
                MetaDataElement ep = GetTokenElement(entryPoint);
                if (ep is MethodDef)
                    ((MethodDef)ep).DeclareEntryPoint();
                else
                    ((ModuleFile)ep).SetEntryPoint();
            }
        }

      /// <summary>
      /// This method saves any *unmanaged* resources in the input PE-file 
      /// to the PEResourcesDirectory field PEFile.unmanagedResourceRoot.
      /// These should be written out to the .rscr section in the PE-file.
      /// Managed resources appear as ManifestResouces in metadata, and are
      /// handled completely differently.
      /// </summary>
        private void SaveUnmanagedResources() {
          if (this.DataDirectorySize[2] != 0) {
            uint resourceRVA = this.DataDirectoryRVA[2];
            uint fileOffset = this.GetOffset(resourceRVA);
            long savedPos = this.BaseStream.Position;
            this.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
            PEFile client = thisScope as PEFile;
            if (client != null) {
              client.unmanagedResourceRoot = new PEResourceDirectory();
              client.unmanagedResourceRoot.PopulateResourceDirectory(this, fileOffset);
            }
            this.BaseStream.Seek(savedPos, SeekOrigin.Begin);
          }
        }

        internal uint GetIndex(MDTable tabIx)
        {
            if (md.largeIx[(int)tabIx]) return ReadUInt32();
            return ReadUInt16();
        }

        internal uint GetCodedIndex(CIx codedIx)
        {
            if (md.lgeCIx[(int)codedIx]) return ReadUInt32();
            return ReadUInt16();
        }

        internal uint GetTableSize(MDTable tabIx)
        {
            return (uint)tableLengths[(int)tabIx];
        }

        internal byte[] GetResource(uint offset)
        {
            BaseStream.Position = GetOffset(resourcesRVA) + offset;
            uint resSize = ReadUInt32();
            return ReadBytes((int)resSize);
        }

        internal MetaDataElement GetTokenElement(uint token)
        {
            uint tabIx = (token & FileImage.TableMask) >> 24;
            uint elemIx = (token & FileImage.ElementMask) - 1;
            MetaDataElement elem = (MetaDataElement)tables[tabIx][(int)elemIx];
            if ((elem != null) && (elem.unresolved))
            {
                elem.Resolve(this);
                elem = (MetaDataElement)tables[tabIx][(int)elemIx];
            }
            return elem;
        }

        internal MetaDataElement GetElement(MDTable tabIx, uint ix)
        {
            if (ix == 0) return null;
            MetaDataElement elem = (MetaDataElement)tables[(int)tabIx][(int)ix - 1];
            if ((elem != null) && (elem.unresolved))
            {
                elem.Resolve(this);
                elem = (MetaDataElement)tables[(int)tabIx][(int)ix - 1];
            }
            return elem;
        }

        internal MetaDataElement GetCodedElement(CIx code, uint ix)
        {
            uint mask = (uint)MetaData.CIxBitMasks[MetaData.CIxShiftMap[(uint)code]];
            int tabIx = MetaData.CIxTables[(int)code][(ix & mask)];
            ix >>= MetaData.CIxShiftMap[(uint)code];
            if (ix == 0) return null;
            MetaDataElement elem = (MetaDataElement)tables[tabIx][(int)ix - 1];
            if ((elem != null) && (elem.unresolved))
            {
                elem.Resolve(this);
                elem = (MetaDataElement)tables[tabIx][(int)ix - 1];
            }
            return elem;
        }

        internal uint MakeCodedIndex(CIx code, MDTable tab, uint ix)
        {
            ix <<= MetaData.CIxShiftMap[(uint)code];
            ix &= (uint)tab;
            return ix;
        }

        internal MDTable CodedTable(CIx code, uint ix)
        {
            uint mask = (uint)MetaData.CIxBitMasks[MetaData.CIxShiftMap[(uint)code]];
            return (MDTable)MetaData.CIxTables[(int)code][(ix & mask)];
        }

        internal uint CodedIndex(CIx code, uint ix)
        {
            ix >>= MetaData.CIxShiftMap[(uint)code];
            return ix;
        }

        internal byte[] GetBlob()
        {
            /* pre:  buffer is at correct position to read blob index */
            uint ix;
            if (md.largeBlob) ix = ReadUInt32();
            else ix = ReadUInt16();
            return blob.GetBlob(ix);
        }

        internal byte[] GetBlob(uint ix)
        {
            return blob.GetBlob(ix);
        }

        internal uint GetBlobIx()
        {
            /* pre:  buffer is at correct position to read blob index */
            //if (Diag.CADiag) Console.WriteLine("Getting blob index at " + BaseStream.Position);
            if (md.largeBlob) return ReadUInt32();
            return ReadUInt16();
        }

        internal byte FirstBlobByte(uint ix)
        {
            blob.GoToIndex(ix);
            uint blobSize = blob.ReadCompressedNum();
            return blob.ReadByte();
        }

        internal Constant GetBlobConst(int constType)
        {
            uint ix;
            if (md.largeBlob) ix = ReadUInt32();
            else ix = ReadUInt16();
            blob.GoToIndex(ix);
            uint blobSize = blob.ReadCompressedNum();
            if (constType == (int)ElementType.String)
                return new StringConst(blob.ReadBytes((int)blobSize));
            return ReadConst(constType, blob);
        }

        /*
        internal Constant ReadConstBlob(int constType, uint blobIx) {
          blob.GoToIndex(blobIx);
          Console.WriteLine("Reading constant blob at index " + blobIx );
          uint blobSize = blob.ReadCompressedNum();
          Console.WriteLine("Got constant blob size of " + blobSize);
          return ReadConst(constType);
        }
        */

        internal static Constant ReadConst(int constType, BinaryReader blob)
        {
            switch (constType)
            {
                case ((int)ElementType.Boolean):
                    return new BoolConst(blob.ReadByte() != 0);
                case ((int)ElementType.Char):
                    return new CharConst(blob.ReadChar());
                case ((int)ElementType.I1):
                    return new IntConst(blob.ReadSByte());
                case ((int)ElementType.U1):
                    return new UIntConst(blob.ReadByte());
                case ((int)ElementType.I2):
                    return new IntConst(blob.ReadInt16());
                case ((int)ElementType.U2):
                    return new UIntConst(blob.ReadUInt16());
                case ((int)ElementType.I4):
                    return new IntConst(blob.ReadInt32());
                case ((int)ElementType.U4):
                    return new UIntConst(blob.ReadUInt32());
                case ((int)ElementType.I8):
                    return new IntConst(blob.ReadInt64());
                case ((int)ElementType.U8):
                    return new UIntConst(blob.ReadUInt64());
                case ((int)ElementType.R4):
                    return new FloatConst(blob.ReadSingle());
                case ((int)ElementType.R8):
                    return new DoubleConst(blob.ReadDouble());
                case ((int)ElementType.ClassType):
                    return new ClassTypeConst(blob.ReadString());  //GetBlobString()); 
                case ((int)ElementType.String):
                    return new StringConst(blob.ReadString());  //GetBlobString()); 
                case ((int)ElementType.Class):
                    return new ClassTypeConst(blob.ReadString());  //GetBlobString()); 
                //uint junk = blob.ReadUInt32();  // need to read name??
                //return new NullRefConst();
                case ((int)ElementType.ValueType):  // only const value type is enum??
                    return new IntConst(blob.ReadInt32());

                default: return null;
            }
        }

        internal string GetBlobString()
        {
            uint ix;
            if (md.largeBlob) ix = ReadUInt32();
            else ix = ReadUInt16();
            return blob.GetBlobString(ix);
        }

        internal string GetString()
        {
            uint ix;
            if (md.largeStrings) ix = ReadUInt32();
            else ix = ReadUInt16();
            return strings.GetString(ix);
        }

        internal string GetString(uint ix)
        {
            return strings.GetString(ix);
        }

        internal uint GetStringIx()
        {
            if (md.largeStrings) return ReadUInt32();
            else return ReadUInt16();
        }

        internal uint GetGUIDIx()
        {
            /* pre:  buffer is at correct position to read GUID index */
            if (md.largeGUID) return ReadUInt32();
            return ReadUInt16();
        }

        public Guid GetGUID()
        {
            uint ix;
            if (md.largeGUID) ix = ReadUInt32();
            else ix = ReadUInt16();
            return new Guid(guid.GetBlob(((ix - 1) * 16), 16));
        }

        public string GetUserString()
        {
            uint ix;
            if (md.largeUS) ix = ReadUInt32();
            else ix = ReadUInt16();
            return userstring.GetUserString(ix);
        }

        internal bool IsFieldSig(uint blobIx)
        {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            byte fldByte = blob.ReadByte();
            return fldByte == Field.FieldTag;
        }

        internal MethSig ReadMethSig(Method thisMeth, uint blobIx)
        {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            return ReadMethSig(thisMeth, false);
        }

        internal MethSig ReadMethSig(Method thisMeth, string name, uint blobIx)
        {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            MethSig mSig = ReadMethSig(thisMeth, false);
            mSig.name = name;
            return mSig;
        }

        private MethSig ReadMethSig(Method currMeth, bool firstByteRead)
        {
            MethSig meth = new MethSig(null);
            if (!firstByteRead)
            {
                byte firstByte = blob.ReadByte();
                if (firstByte == Field.FieldTag)
                    return null;
                meth.callConv = (CallConv)firstByte;
            }
            if ((meth.callConv & CallConv.Generic) != 0)
            {
                meth.numGenPars = blob.ReadCompressedNum();
                if (currMeth is MethodRef)
                {
                    ((MethodRef)currMeth).MakeGenericPars(meth.numGenPars);
                }
            }
            uint parCount = blob.ReadCompressedNum();
            if (Diag.DiagOn) Console.WriteLine("Method sig has " + parCount + " parameters");
            meth.retType = GetBlobType();//currClass,currMeth);
            if (meth.retType == null)
                System.Diagnostics.Debug.Assert(meth.retType != null);
            int optParStart = -1;
            ArrayList pTypes = new ArrayList();
            for (int i = 0; i < parCount; i++)
            {
                Type pType = GetBlobType();//currClass,currMeth);
                if (pType == sentinel)
                {
                    optParStart = i;
                    pType = GetBlobType();//currClass,currMeth);
                }
                if (Diag.DiagOn) if (pType == null) Console.WriteLine("Param type is null");
                pTypes.Add(pType);
            }
            if (optParStart > -1)
            {
                meth.numPars = (uint)optParStart;
                meth.numOptPars = parCount - meth.numPars;
                meth.optParTypes = new Type[meth.numOptPars];
                for (int i = 0; i < meth.numOptPars; i++)
                {
                    meth.optParTypes[i] = (Type)pTypes[i + optParStart];
                }
            }
            else
                meth.numPars = parCount;
            meth.parTypes = new Type[meth.numPars];
            for (int i = 0; i < meth.numPars; i++)
            {
                meth.parTypes[i] = (Type)pTypes[i];
            }
            return meth;
        }

        internal Type[] ReadMethSpecSig(uint blobIx)
        { //ClassDef currClass, Method currMeth, uint blobIx) {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            if (blob.ReadByte() != MethodSpec.GENERICINST)
                throw new Exception("Not a MethodSpec signature");
            return GetListOfType(); //currClass,currMeth);
        }

        internal Type GetFieldType(uint blobIx)
        {
            //Console.WriteLine("Getting field type");
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            byte fldByte = blob.ReadByte();
            if (fldByte != 0x6)
                throw new Exception("Expected field signature");
            //if ((currClass != null) && (currClass is ClassRef))
            //  currClass = null;
            return GetBlobType(); //currClass,null);
        }

        internal Type GetBlobType(uint blobIx)
        { //Class currClass, Method currMeth, uint blobIx) {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            return GetBlobType(); //currClass,currMeth);
        }

        private Type[] GetListOfType()
        { //Class currClass | Method currMeth) {
            uint numPars = blob.ReadCompressedNum();
            Type[] gPars = new Type[numPars];
            for (int i = 0; i < numPars; i++)
            {
                gPars[i] = GetBlobType(); //currClass|currMeth);
            }
            return gPars;
        }

        private Type GetBlobType()
        { //Class currClass, Method currMeth) {
            byte typeIx = blob.ReadByte();
            if (Diag.DiagOn) Console.WriteLine("Getting blob type " + (ElementType)typeIx);
            if (typeIx < PrimitiveType.primitives.Length)
                return PrimitiveType.primitives[typeIx];
            switch (typeIx)
            {
                case ((int)ElementType.Ptr):
                    return new UnmanagedPointer(GetBlobType()); //currClass,currMeth)); 
                case ((int)ElementType.ByRef):
                    return new ManagedPointer(GetBlobType()); //currClass,currMeth)); 
                case ((int)ElementType.ValueType):
                    //Console.WriteLine("Reading value type");
                    uint vcIx = blob.ReadCompressedNum();
                    Class vClass = (Class)GetCodedElement(CIx.TypeDefOrRef, vcIx);
                    vClass.MakeValueClass();
                    return vClass;
                case ((int)ElementType.Class):
                    return (Class)GetCodedElement(CIx.TypeDefOrRef, blob.ReadCompressedNum());
                case ((int)ElementType.Array):
                    Type elemType = GetBlobType(); //currClass,currMeth);
                    int rank = (int)blob.ReadCompressedNum();
                    int numSizes = (int)blob.ReadCompressedNum();
                    int[] sizes = null;
                    int[] hiBounds = null;
                    if (numSizes > 0)
                    {
                        sizes = new int[numSizes];
                        hiBounds = new int[numSizes];
                        for (int i = 0; i < numSizes; i++)
                            sizes[i] = (int)blob.ReadCompressedNum();
                    }
                    int numLoBounds = (int)blob.ReadCompressedNum();
                    int[] loBounds = null;
                    // 
                    // We have the constraints: 
                    //     0 <= numSizes <= numLoBounds <= rank
                    //                          
                    if (numLoBounds > 0)
                    {
                      int constraint = (numLoBounds < numSizes ? numSizes : numLoBounds);
                      loBounds = new int[constraint];
                        //loBounds = new int[numLoBounds];
                        for (int i = 0; i < numLoBounds; i++)
                            loBounds[i] = blob.ReadCompressedInt();
                        if (numSizes > 0)
                            for (int i = 0; i < numSizes; i++)
                                hiBounds[i] = loBounds[i] + sizes[i] - 1;
                    }
                    if (numLoBounds == 0) // Implies numSizes == 0 also 
                        return new BoundArray(elemType, rank);
                    else 
                        return new BoundArray(elemType, rank, loBounds, hiBounds);

                case ((int)ElementType.TypedByRef):
                    return PrimitiveType.TypedRef;
                case ((int)ElementType.I):
                    return PrimitiveType.IntPtr;
                case ((int)ElementType.U):
                    return PrimitiveType.UIntPtr;
                case ((int)ElementType.FnPtr):
                    MethSig mSig = ReadMethSig(null, false);
                    return new MethPtrType(mSig);
                case ((int)ElementType.Object):
                    return PrimitiveType.Object;
                case ((int)ElementType.SZArray):
                    return new ZeroBasedArray(GetBlobType()); //currClass,currMeth));
                case ((int)ElementType.CmodReqd):
                case ((int)ElementType.CmodOpt):
                    Class modType = (Class)GetCodedElement(CIx.TypeDefOrRef, blob.ReadCompressedNum());
                    return new CustomModifiedType(GetBlobType(), (CustomModifier)typeIx, modType);
                case ((int)ElementType.Sentinel):
                    return sentinel;
                case ((int)ElementType.Pinned):
                    return pinned;
                case ((int)ElementType.GenericInst):
                    Class instType = (Class)GetBlobType();
                    Class scopeSave = currentClassScope;
                    if (genInstNestLevel > 0)
                    {
                        currentClassScope = instType;
                    }
                    genInstNestLevel++;
                    ClassSpec newClassSpec = new ClassSpec(instType, GetListOfType());
                    genInstNestLevel--;
                    if (genInstNestLevel > 0)
                    {
                        currentClassScope = scopeSave;
                    }
                    return newClassSpec;
                case ((int)ElementType.Var):
                    if (currentClassScope == null)
                    {
                        //Console.WriteLine("GenericParam with currClass == null");
                        return GenericParam.AnonClassPar(blob.ReadCompressedNum());
                        //throw new Exception("No current class set");
                    }
                    return currentClassScope.GetGenPar(blob.ReadCompressedNum());
                case ((int)ElementType.MVar):
                    if (currentMethodScope == null)
                    {
                        //Console.WriteLine("GenericParam with currMeth == null");
                        return GenericParam.AnonMethPar(blob.ReadCompressedNum());
                        //throw new Exception("No current method set");
                    }
                    return currentMethodScope.GetGenericParam((int)blob.ReadCompressedNum());
                default: break;
            }
            return null;
        }

        internal NativeType GetBlobNativeType(uint blobIx)
        {
            blob.GoToIndex(blobIx);
            uint blobSize = blob.ReadCompressedNum();
            return GetBlobNativeType();
        }

        internal NativeType GetBlobNativeType()
        {
            byte typeIx = blob.ReadByte();
            if (typeIx == (byte)NativeTypeIx.Array)
            {
                return new NativeArray(GetBlobNativeType(), blob.ReadCompressedNum(),
                    blob.ReadCompressedNum(), blob.ReadCompressedNum());
            }
            else
                return NativeType.GetNativeType(typeIx);
        }

        internal Local[] ReadLocalSig(uint sigIx)
        { //Class currClass, Method currMeth, uint sigIx) {
            blob.GoToIndex(sigIx);
            uint blobSize = blob.ReadCompressedNum();
            if (blob.ReadByte() != LocalSig.LocalSigByte) InputError();
            uint count = blob.ReadCompressedNum();
            Local[] locals = new Local[count];
            for (uint i = 0; i < count; i++)
            {
                Type lType = GetBlobType(); //currClass,currMeth);
                bool pinnedLocal = lType == pinned;
                if (pinnedLocal) lType = GetBlobType(); //currClass,currMeth);
                locals[i] = new Local("loc" + i, lType, pinnedLocal);
            }
            return locals;
        }

        internal void ReadPropertySig(uint sigIx, Property prop)
        {
            blob.GoToIndex(sigIx);
            uint blobSize = blob.ReadCompressedNum();
            if ((blob.ReadByte() & Property.PropertyTag) != Property.PropertyTag) InputError();
            uint count = blob.ReadCompressedNum();
            Type[] pars = new Type[count];
            prop.SetPropertyType(GetBlobType()); //prop.GetParent(),null));
            for (int i = 0; i < count; i++)
                pars[i] = GetBlobType(); //prop.GetParent(),null);
            prop.SetPropertyParams(pars);
        }

        internal DataConstant GetDataConstant(uint rva, Type constType) {
          ManagedPointer pointer = null;
          ClassDef image = null;
          BaseStream.Seek(GetOffset(rva), SeekOrigin.Begin);
          if (constType is PrimitiveType) {
            switch (constType.GetTypeIndex()) {
              case ((int)ElementType.I1): return new IntConst(ReadByte());
              case ((int)ElementType.I2): return new IntConst(ReadInt16());
              case ((int)ElementType.I4): return new IntConst(ReadInt32());
              case ((int)ElementType.I8): return new IntConst(ReadInt64());
              case ((int)ElementType.R4): return new FloatConst(ReadSingle());
              case ((int)ElementType.R8): return new DoubleConst(ReadDouble());
              case ((int)ElementType.String): return new StringConst(ReadString());
            }
          }
          else if ((pointer = constType as ManagedPointer) != null) {
            uint dataRVA = ReadUInt32();
            Type baseType = pointer.GetBaseType();
            return new AddressConstant(GetDataConstant(dataRVA, baseType));
          } // need to do repeated constant??
          else if ((image = constType as ClassDef) != null && image.Layout != null) {
            byte[] data = new byte[image.Layout.GetSize()];
            for (int i = 0; i < data.Length; i++)
              data[i] = ReadByte();
            return new ByteArrConst(data);
          }
          return null;
        }

        internal ModuleFile GetFileDesc(string name)
        {
            if (tables[(int)MDTable.File] == null) return null;
            for (int i = 0; i < tables[(int)MDTable.File].Length; i++)
            {
                FileRef fr = (FileRef)tables[(int)MDTable.File][i];
                if (fr.Name() == name)
                {
                    if (fr is ModuleFile) return (ModuleFile)fr;
                    fr = new ModuleFile(fr.Name(), fr.GetHash());
                    tables[(int)MDTable.File][i] = fr;
                    return (ModuleFile)fr;
                }
            }
            return null;
        }

        /*
        private long GetOffset(int rva) {
          for (int i=0; i < inputSections.Length; i++) {
            long offs = inputSections[i].GetOffset(rva);
            if (offs > 0) return offs;
          }
          return 0;
        }

        public bool ReadPadding(int boundary) {
          while ((Position % boundary) != 0) {
            if (buffer[index++] != 0) { return false; }
          }
          return true;
        }

        public String ReadName() {
          int len = NAMELEN;
          char [] nameStr = new char[NAMELEN];
          char ch = (char)ReadByte();
          int i=0;
          for (; (i < NAMELEN) && (ch != '\0'); i++) {
            nameStr[i] = ch;
            ch = (char)ReadByte();
          }
          return new String(nameStr,0,i);
        }

        internal String ReadString() {
          char [] str = new char[STRLEN];
          int i=0;
          char ch = (char)ReadByte();
          for (; ch != '\0'; i++) {
            str[i] = ch;
            ch = (char)ReadByte();
          }
          return new String(str,0,i);
        }

        public long GetPos() {
          return BaseStream.Position;
        }

        public void SetPos(int ix) {
          BaseStream.Position = ix;
        }
    */
        /*
        public void SetToRVA(int rva) {
          index = PESection.GetOffset(rva);
    //      Console.WriteLine("Setting buffer to rva " + PEConsts.Hex(rva) + " = index " + PEConsts.Hex(index));
    //      Console.WriteLine("Setting buffer to rva " + rva + " = index " + index);
        }

        public byte[] GetBuffer() {
          return buffer;
        }
    */
        private CILInstruction[] DoByteCodes(uint len, MethodDef thisMeth)
        {
            uint pos = 0;
            ArrayList instrList = new ArrayList();
            //int instrIx = 0;
            while (pos < len)
            {
                uint offset = pos;
                uint opCode = ReadByte();
                pos++;
                IType iType = IType.op;
                if (opCode == 0xFE)
                {
                    uint ix = ReadByte();
                    pos++;
                    opCode = (opCode << 8) + ix;
                    iType = FileImage.longInstrMap[ix];
                }
                else
                    iType = FileImage.instrMap[opCode];
                if (Diag.DiagOn) Console.WriteLine("Got instruction type " + iType);
                CILInstruction nextInstr = null;
                if (iType == IType.specialOp)
                {
                    pos += 4;
                    if (Diag.DiagOn) Console.WriteLine("Got instruction " + Hex.Byte((int)opCode));
                    switch (opCode)
                    {
                        case ((int)SpecialOp.ldc_i8):
                            nextInstr = new LongInstr((SpecialOp)opCode, ReadInt64());
                            pos += 4; break;
                        case ((int)SpecialOp.ldc_r4):
                            nextInstr = new FloatInstr((SpecialOp)opCode, ReadSingle());
                            break;
                        case ((int)SpecialOp.ldc_r8):
                            nextInstr = new DoubleInstr((SpecialOp)opCode, ReadDouble());
                            pos += 4; break;
                        case ((int)SpecialOp.calli):
                            nextInstr = new SigInstr((SpecialOp)opCode, (CalliSig)GetTokenElement(ReadUInt32()));
                            break;
                        case ((int)SpecialOp.Switch): // switch
                            uint count = ReadUInt32();
                            int[] offsets = new int[count];
                            for (uint i = 0; i < count; i++)
                                offsets[i] = ReadInt32();
                            pos += (4 * count);
                            nextInstr = new SwitchInstr(offsets);
                            break;
                        case ((int)SpecialOp.ldstr): // ldstr
                            uint strIx = ReadUInt32();
                            strIx = strIx & FileImage.ElementMask;
                            nextInstr = new StringInstr((SpecialOp)opCode, userstring.GetUserString(strIx));
                            break;
                        case ((int)MethodOp.ldtoken):
                            MetaDataElement elem = GetTokenElement(ReadUInt32());
                            if (elem is Method)
                                nextInstr = new MethInstr((MethodOp)opCode, (Method)elem);
                            else if (elem is Field)
                                nextInstr = new FieldInstr((FieldOp)opCode, (Field)elem);
                            else
                                nextInstr = new TypeInstr((TypeOp)opCode, (Type)elem);
                            break;
                    }
                }
                else if (iType == IType.branchOp)
                {
                    if (Diag.DiagOn) Console.WriteLine("Got instruction " + Hex.Byte((int)opCode));
                    if ((opCode < 0x38) || (opCode == 0xDE))
                    { // br or leave.s
                        nextInstr = new BranchInstr(opCode, ReadSByte());
                        pos++;
                    }
                    else
                    {
                        nextInstr = new BranchInstr(opCode, ReadInt32());
                        pos += 4;
                    }
                }
                else
                {
                    if (Diag.DiagOn) Console.Write(Hex.Byte((int)opCode));
                    switch (iType)
                    {
                        case (IType.op):
                            if (Diag.DiagOn) Console.WriteLine("Got instruction " + (Op)opCode);
                            nextInstr = new Instr((Op)opCode); break;
                        case (IType.methOp):
                            if (Diag.DiagOn) Console.WriteLine("Got instruction " + (MethodOp)opCode);
                            nextInstr = new MethInstr((MethodOp)opCode, (Method)GetTokenElement(ReadUInt32()));
                            pos += 4;
                            break;
                        case (IType.typeOp):
                            if (Diag.DiagOn) Console.WriteLine("Got instruction " + (TypeOp)opCode);
                            uint ttok = ReadUInt32();
                            Type typeToken = (Type)GetTokenElement(ttok);
                            if (typeToken is GenericParTypeSpec)
                                typeToken = ((GenericParTypeSpec)typeToken).GetGenericParam(thisMeth);
                            nextInstr = new TypeInstr((TypeOp)opCode, typeToken);
                            pos += 4;
                            break;
                        case (IType.fieldOp):
                            if (Diag.DiagOn) Console.WriteLine("Got instruction " + (FieldOp)opCode);
                            nextInstr = new FieldInstr((FieldOp)opCode, (Field)GetTokenElement(ReadUInt32()));
                            pos += 4;
                            break;
                        case (IType.int8Op):
                            nextInstr = new IntInstr((IntOp)opCode, ReadSByte());
                            pos++;
                            break;
                        case (IType.uint8Op):
                            nextInstr = new UIntInstr((IntOp)opCode, ReadByte());
                            pos++;
                            break;
                        case (IType.uint16Op):
                            nextInstr = new UIntInstr((IntOp)opCode, ReadUInt16());
                            pos++;
                            break;
                        case (IType.int32Op):
                            nextInstr = new IntInstr((IntOp)opCode, ReadInt32());
                            pos += 4;
                            break;
                    }
                }
                if (nextInstr != null) nextInstr.Resolve();
                instrList.Add(nextInstr);
            }
            CILInstruction[] instrs = new CILInstruction[instrList.Count];
            for (int i = 0; i < instrs.Length; i++)
            {
                instrs[i] = (CILInstruction)instrList[i];
            }
            return instrs;
        }

        public void ReadByteCodes(MethodDef meth, uint rva)
        {
            if (rva == 0) return;
            BaseStream.Seek(GetOffset(rva), SeekOrigin.Begin);
            CILInstructions instrs = meth.CreateCodeBuffer();
            uint formatByte = ReadByte();
            uint format = formatByte & 0x3;
            if (Diag.DiagOn) Console.WriteLine("code header format = " + Hex.Byte((int)formatByte));
            uint size = 0;
            if (format == CILInstructions.TinyFormat)
            {
                size = formatByte >> 2;
                if (Diag.DiagOn) Console.WriteLine("Tiny Format, code size = " + size);
                instrs.SetAndResolveInstructions(DoByteCodes(size, meth));
            }
            else if (format == CILInstructions.FatFormat)
            {
                uint headerSize = ReadByte();
                bool initLocals = (formatByte & CILInstructions.InitLocals) != 0;
                bool moreSects = (formatByte & CILInstructions.MoreSects) != 0;
                meth.SetMaxStack((int)ReadUInt16());
                size = ReadUInt32();
                if (Diag.DiagOn) Console.WriteLine("Fat Format, code size = " + size);
                uint locVarSig = ReadUInt32();
                CILInstruction[] instrList = this.DoByteCodes(size, meth);
                while (moreSects)
                {
                    // find next 4 byte boundary
                    long currPos = BaseStream.Position;
                    if (currPos % 4 != 0)
                    {
                        long pad = 4 - (currPos % 4);
                        for (int p = 0; p < pad; p++)
                            ReadByte();
                    }
                    uint flags = ReadByte();
                    //while (flags == 0) flags = ReadByte();  // maximum of 3 to get 4 byte boundary??
                    moreSects = (flags & CILInstructions.SectMoreSects) != 0;
                    bool fatSect = (flags & CILInstructions.SectFatFormat) != 0;
                    if ((flags & CILInstructions.EHTable) == 0)
                        throw new Exception("Section not an Exception Handler Table");
                    int sectLen = ReadByte() + (ReadByte() << 8) + (ReadByte() << 16);
                    int numClauses = sectLen - 4;
                    if (fatSect)
                        numClauses /= 24;
                    else
                        numClauses /= 12;
                    for (int i = 0; i < numClauses; i++)
                    {
                        EHClauseType eFlag;
                        if (fatSect) eFlag = (EHClauseType)ReadUInt32();
                        else eFlag = (EHClauseType)ReadUInt16();
                        uint tryOff = 0, tryLen = 0, hOff = 0, hLen = 0;
                        if (fatSect)
                        {
                            tryOff = ReadUInt32();
                            tryLen = ReadUInt32();
                            hOff = ReadUInt32();
                            hLen = ReadUInt32();
                        }
                        else
                        {
                            tryOff = ReadUInt16();
                            tryLen = ReadByte();
                            hOff = ReadUInt16();
                            hLen = ReadByte();
                        }
                        EHClause ehClause = new EHClause(eFlag, tryOff, tryLen, hOff, hLen);
                        if (eFlag == EHClauseType.Exception)
                            ehClause.ClassToken(GetTokenElement(ReadUInt32()));
                        else
                            ehClause.FilterOffset(ReadUInt32());
                        instrs.AddEHClause(ehClause);
                    }
                }
                if (locVarSig != 0)
                {
                    LocalSig lSig = (LocalSig)GetTokenElement(locVarSig);
                    lSig.Resolve(this, meth);
                    meth.AddLocals(lSig.GetLocals(), initLocals);
                }
                instrs.SetAndResolveInstructions(instrList);
            }
            else
            {
                Console.WriteLine("byte code format error");
            }
        }

    }



}