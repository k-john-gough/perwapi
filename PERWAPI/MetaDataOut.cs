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
using System.Text;
using System.Collections;


namespace QUT.PERWAPI
{
    /**************************************************************************/
    // MetaData generated from user created descriptors
    /**************************************************************************/
    internal class MetaDataOut : MetaData
    {
        MetaDataStream strings, us, guid, blob;
        MetaDataStream[] streams;
        uint numStreams = 5;
        uint tildeTide = 0, tildePadding = 0, tildeStart = 0;
        uint numTables = 0, resourcesSize = 0;
        ArrayList byteCodes = new ArrayList();
        uint codeSize = 0, byteCodePadding = 0, metaDataSize = 0;
        internal PEWriter output;
        private byte heapSizes = 0;
        MetaDataElement entryPoint;
        long mdStart;
        ArrayList resources;
        private ArrayList[] tables = new ArrayList[NumMetaDataTables];

        // Allow the debug mode to be set.
        public bool Debug = false;

        internal MetaDataOut()
            : base()
        {
        }

        Hashtable debugsigs = new Hashtable();

        /// <summary>
        /// Get the debug signature for a local.
        /// </summary>
        /// <param name="loc">The local.</param>
        /// <returns>The signature.</returns>
        internal DebugLocalSig GetDebugSig(Local loc)
        {
            byte[] b = loc.GetSig();
            string s = BitConverter.ToString(b);
            DebugLocalSig sig = (DebugLocalSig)debugsigs[s];
            if (sig != null) return sig;
            sig = new DebugLocalSig(b);
            debugsigs.Add(s, sig);
            return sig;
        }

        internal void InitMetaDataOut(PEWriter file)
        {
            // tilde = new MetaDataStream(tildeNameArray,false,0);
            this.output = file;
            streams = new MetaDataStream[5];
            strings = new MetaDataStream(MetaData.stringsNameArray, new UTF8Encoding(), true);
            us = new MetaDataStream(MetaData.usNameArray, new UnicodeEncoding(), true);
            guid = new MetaDataStream(MetaData.guidNameArray, false);
            blob = new MetaDataStream(MetaData.blobNameArray, new UnicodeEncoding(), true);
            streams[1] = strings;
            streams[2] = us;
            streams[3] = guid;
            streams[4] = blob;
        }

        internal uint Size()
        {
            //Console.WriteLine("metaData size = " + metaDataSize);
            return metaDataSize;
        }

        internal uint AddToUSHeap(string str)
        {
            if (str == null) return 0;
            return us.Add(str, true);
        }

        internal uint AddToStringsHeap(string str)
        {
            if ((str == null) || (str == "")) return 0;
            return strings.Add(str, false);
        }

        internal uint AddToGUIDHeap(Guid guidNum)
        {
            return guid.Add(guidNum);
        }

        internal uint AddToBlobHeap(byte[] blobBytes)
        {
            if (blobBytes == null) return 0;
            return blob.Add(blobBytes);
        }

        internal uint AddToBlobHeap(long val, uint numBytes)
        {
            return blob.Add(val, numBytes);
        }

        internal uint AddToBlobHeap(ulong val, uint numBytes)
        {
            return blob.Add(val, numBytes);
        }

        internal uint AddToBlobHeap(char ch)
        {
            return blob.Add(ch);
        }

        /*
        internal uint AddToBlobHeap(byte val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(sbyte val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(ushort val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(short val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(uint val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(int val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(ulong val) {
          return blob.Add(val);
        }

        internal uint AddToBlobHeap(long val) {
          return blob.Add(val);
        }
        */

        internal uint AddToBlobHeap(float val)
        {
            return blob.Add(val);
        }

        internal uint AddToBlobHeap(double val)
        {
            return blob.Add(val);
        }

        internal uint AddToBlobHeap(string val)
        {
            return blob.Add(val, true);
        }

        private ArrayList GetTable(MDTable tableIx)
        {
            int tabIx = (int)tableIx;
            if (tables[tabIx] == null)
            {
                tables[tabIx] = new ArrayList();
                valid |= ((ulong)0x1 << tabIx);
                // Console.WriteLine("after creating table " + tableIx + "(" + tabIx + ") valid = " + valid);
                numTables++;
            }
            return tables[tabIx];
        }

        internal void AddToTable(MDTable tableIx, MetaDataElement elem)
        {
            // updates Row field of the element
            // Console.WriteLine("Adding element to table " + (uint)tableIx);
            ArrayList table = GetTable(tableIx);
            if (table.Contains(elem))
            {
                Console.Out.WriteLine("ERROR - element already in table " + tableIx);
                return;
            }
            elem.Row = (uint)table.Count + 1;
            table.Add(elem);
        }

        internal uint TableIndex(MDTable tableIx)
        {
            if (tables[(int)tableIx] == null) return 1;
            return (uint)tables[(int)tableIx].Count + 1;
        }

        internal uint AddCode(CILInstructions byteCode)
        {
            byteCodes.Add(byteCode);
            uint offset = codeSize;
            codeSize += byteCode.GetCodeSize();
            return offset;
        }

        internal void SetEntryPoint(MetaDataElement ep)
        {
            entryPoint = ep;
        }

        internal uint AddResource(byte[] resBytes)
        {
            if (resources == null) resources = new ArrayList();
            resources.Add(resBytes);
            uint offset = resourcesSize;
            resourcesSize += (uint)resBytes.Length + 4;
            return offset;
        }

        internal void AddData(DataConstant cVal)
        {
            output.AddInitData(cVal);
        }

        internal static void CompressNum(byte[] arr, MemoryStream sig)
        {
            for (int ix = 0; ix < arr.Length; ix++) sig.WriteByte(arr[ix]);
        }

        internal uint CodeSize()
        {
            return codeSize + byteCodePadding;
        }

        internal uint GetResourcesSize() { return resourcesSize; }

        private void SetStreamOffsets()
        {
            uint sizeOfHeaders = StreamHeaderSize + (uint)tildeNameArray.Length;
            for (int i = 1; i < numStreams; i++)
            {
                sizeOfHeaders += streams[i].headerSize();
            }
            metaDataSize = MetaDataHeaderSize + sizeOfHeaders;
            //Console.WriteLine("Size of meta data headers (tildeStart) = " + Hex.Long(metaDataSize));
            tildeStart = metaDataSize;
            metaDataSize += tildeTide + tildePadding;
            //Console.WriteLine(tildeNameArray + " - size = " + (tildeTide + tildePadding));
            for (int i = 1; i < numStreams; i++)
            {
                //Console.WriteLine("Stream " + i + " " + new String(streams[i].name) + " starts at " + Hex.Long(metaDataSize));
                streams[i].Start = metaDataSize;
                metaDataSize += streams[i].Size();
                streams[i].WriteDetails();
            }
            if (largeStrings) heapSizes |= 0x01;
            if (largeGUID) heapSizes |= 0x02;
            if (largeBlob) heapSizes |= 0x04;
        }

        internal void CalcTildeStreamSize()
        {
            largeStrings = strings.LargeIx();
            largeBlob = blob.LargeIx();
            largeGUID = guid.LargeIx();
            largeUS = us.LargeIx();
            CalcElemSize();
            //tilde.SetIndexSizes(strings.LargeIx(),us.LargeIx(),guid.LargeIx(),blob.LargeIx());
            tildeTide = TildeHeaderSize;
            tildeTide += 4 * numTables;
            //Console.WriteLine("Tilde header + sizes = " + tildeTide);
            for (int i = 0; i < NumMetaDataTables; i++)
            {
                if (tables[i] != null)
                {
                    ArrayList table = tables[i];
                    // Console.WriteLine("Meta data table " + i + " at offset " + tildeTide);
                    tildeTide += (uint)table.Count * elemSize[i];
                    // Console.WriteLine("Metadata table " + i + " has size " + table.Count);
                    // Console.WriteLine("tildeTide = " + tildeTide);
                }
            }
            if ((tildeTide % 4) != 0) tildePadding = 4 - (tildeTide % 4);
            //Console.WriteLine("tildePadding = " + tildePadding);
        }

        internal void WriteTildeStream(PEWriter output)
        {
            long startTilde = output.Seek(0, SeekOrigin.Current);
            //Console.WriteLine("Starting tilde output at offset " + Hex.Long(startTilde));
            output.Write((uint)0); // Reserved
            output.Write(output.verInfo.tsMajVer); // MajorVersion
            output.Write(output.verInfo.tsMinVer); // MinorVersion
            output.Write(heapSizes);
            output.Write((byte)1); // Reserved
            output.Write(valid);
            output.Write(sorted);
            for (int i = 0; i < NumMetaDataTables; i++)
            {
                if (tables[i] != null)
                {
                    uint count = (uint)tables[i].Count;
                    output.Write(count);
                }
            }
            long tabStart = output.Seek(0, SeekOrigin.Current);
            //Console.WriteLine("Starting metaData tables at " + tabStart);
            for (int i = 0; i < NumMetaDataTables; i++)
            {
                if (tables[i] != null)
                {
                    //Console.WriteLine("Starting metaData table " + i + " at " + (output.Seek(0,SeekOrigin.Current) - startTilde));
                    ArrayList table = tables[i];
                    for (int j = 0; j < table.Count; j++)
                    {
                        ((MetaDataElement)table[j]).Write(output);
                    }
                }
            }
            // reset the typespec flags
            if (tables[(int)MDTable.TypeSpec] != null)
            {
                ArrayList typeSpecTable = tables[(int)MDTable.TypeSpec];
                for (int i = 0; i < typeSpecTable.Count; i++)
                {
                    ((TypeSpec)typeSpecTable[i]).typeSpecAdded = false;
                }
            }
            //Console.WriteLine("Writing padding at " + output.Seek(0,SeekOrigin.Current));
            for (int i = 0; i < tildePadding; i++) output.Write((byte)0);
        }

        private void SortTable(ArrayList mTable)
        {
            //Console.WriteLine("Sorting table");
            if (mTable == null) return;
            mTable.Sort();
            for (int i = 0; i < mTable.Count; i++)
            {
                ((MetaDataElement)mTable[i]).Row = (uint)i + 1;
            }
        }

        internal void BuildMDTables()
        {
            // Check ordering of specific tables
            // Constant, CustomAttribute, FieldMarshal, DeclSecurity, MethodSemantics
            // ImplMap, NestedClass, GenericParam
            // Need to load GenericParamConstraint AFTER GenericParam table in correct order
            // The tables:
            //   InterfaceImpl, ClassLayout, FieldLayout, MethodImpl, FieldRVA
            // will _ALWAYS_ be in the correct order as embedded in BuildMDTables

            SortTable(tables[(int)MDTable.Constant]);
            SortTable(tables[(int)MDTable.CustomAttribute]);
            SortTable(tables[(int)MDTable.FieldMarshal]);
            SortTable(tables[(int)MDTable.DeclSecurity]);
            SortTable(tables[(int)MDTable.MethodSemantics]);
            SortTable(tables[(int)MDTable.ImplMap]);
            SortTable(tables[(int)MDTable.NestedClass]);
            if (tables[(int)MDTable.GenericParam] != null)
            {
                SortTable(tables[(int)MDTable.GenericParam]);
                // Now add GenericParamConstraints
                for (int i = 0; i < tables[(int)MDTable.GenericParam].Count; i++)
                {
                    ((GenericParam)tables[(int)MDTable.GenericParam][i]).AddConstraints(this);
                }
            }

            /*
            // for bug in Whidbey GenericParam table ordering
            int end = tables[(int)MDTable.TypeDef].Count;
            int methEnd = 0;
            if (tables[(int)MDTable.Method] != null) {
              methEnd = tables[(int)MDTable.Method].Count;
            }
            for (int i=0; i < end; i++) {
              ((ClassDef)tables[(int)MDTable.TypeDef][i]).AddGenericsToTable(this);
              if (methEnd > i)
                ((MethodDef)tables[(int)MDTable.Method][i]).AddGenericsToTable(this);
            }
            for (int i=end; i < methEnd; i++) {
              ((MethodDef)tables[(int)MDTable.Method][i]).AddGenericsToTable(this);
            }
            // end of bug fix
            */
            for (int i = 0; i < tables.Length; i++)
            {
                if (tables[i] != null)
                {
                    for (int j = 0; j < tables[i].Count; j++)
                    {
                        ((MetaDataElement)tables[i][j]).BuildSignatures(this);
                    }
                }
            }
        }

        internal void SetIndexSizes()
        {
            for (int i = 0; i < NumMetaDataTables; i++)
            {
                if (tables[i] != null)
                {
                    largeIx[i] = (uint)tables[i].Count > maxSmlIxSize;
                }
            }
            for (int i = 0; i < CIxTables.Length; i++)
            {
                for (int j = 0; j < CIxTables[i].Length; j++)
                {
                    int tabIx = CIxTables[i][j];
                    if (tables[tabIx] != null)
                    {
                        lgeCIx[i] = lgeCIx[i] | tables[tabIx].Count > CIxMaxMap[i];
                    }
                }
            }
        }

        internal void BuildMetaData()
        {
            SetIndexSizes();
            for (int i = 1; i < numStreams; i++)
            {
                if (streams[i].Size() <= 1)
                {
                    //Console.WriteLine("Stream " + new String(streams[i].name) + " has size 0");
                    for (int j = i + 1; j < numStreams; j++)
                    {
                        streams[i] = streams[j];
                    }
                    i--;
                    numStreams--;
                }
                else
                    streams[i].EndStream();
            }
            //Console.WriteLine("numStreams = " + numStreams);
            CalcTildeStreamSize();
            SetStreamOffsets();
            byteCodePadding = NumToAlign(codeSize, 4);
            if (entryPoint != null) output.SetEntryPoint(entryPoint.Token());
        }

        internal void WriteByteCodes(PEWriter output)
        {
            for (int i = 0; i < byteCodes.Count; i++)
            {
                ((CILInstructions)byteCodes[i]).Write(output);
            }
            for (int i = 0; i < byteCodePadding; i++)
            {
                output.Write((byte)0);
            }
        }

        internal void WriteResources(PEWriter output)
        {
            if (resources == null) return;
            for (int i = 0; i < resources.Count; i++)
            {
                byte[] resBytes = (byte[])resources[i];
                output.Write((uint)resBytes.Length);
                output.Write(resBytes);
            }
        }

        internal void WriteMetaData(PEWriter output)
        {
            this.output = output;
            if (Diag.DiagOn)
            {
                mdStart = output.Seek(0, SeekOrigin.Current);
                Console.WriteLine("Writing metaData at " + Hex.Long(mdStart));
            }
            output.Write(MetaDataSignature);
            output.Write(output.verInfo.mdMajVer);
            output.Write(output.verInfo.mdMinVer);
            output.Write(0);         // Reserved
            output.Write(output.verInfo.netVerString.Length);
            output.Write(output.verInfo.netVerString.ToCharArray());   // version string is already zero padded
            output.Write((short)0);  // Flags, reserved
            output.Write((ushort)numStreams);
            // write tilde header
            output.Write(tildeStart);
            output.Write(tildeTide + tildePadding);
            output.Write(tildeNameArray);
            for (int i = 1; i < numStreams; i++)
            {
                if (Diag.DiagOn)
                    Console.WriteLine("Stream " + new String(streams[i].name) + " should start at " + Hex.Long(streams[i].Start + mdStart));
                streams[i].WriteHeader(output);
            }
            if (Diag.DiagOn)
            {
                Console.Write("Writing tilde stream at " + Hex.Long(output.Seek(0, SeekOrigin.Current)));
                Console.WriteLine(" should be at " + Hex.Long(tildeStart + mdStart));
            }
            WriteTildeStream(output);
            for (int i = 1; i < numStreams; i++)
            {
                if (Diag.DiagOn)
                    Console.WriteLine("Writing stream " + new String(streams[i].name) + " at " + Hex.Long(output.Seek(0, SeekOrigin.Current)));
                streams[i].Write(output);
            }
            //Console.WriteLine("Finished Writing metaData at " + output.Seek(0,SeekOrigin.Current));
        }

        //    internal bool LargeStringsIndex() { return strings.LargeIx(); }
        //    internal bool LargeGUIDIndex() { return guid.LargeIx(); }
        //    internal bool LargeUSIndex() { return us.LargeIx(); }
        //    internal bool LargeBlobIndex() { return blob.LargeIx(); }

        private uint NumToAlign(uint val, uint alignVal)
        {
            if ((val % alignVal) == 0) return 0;
            return alignVal - (val % alignVal);
        }

        internal void WriteCodedIndex(CIx code, MetaDataElement elem, PEWriter output)
        {
            uint ix = 0;
            if (elem != null)
            {
                ix = (elem.Row << CIxShiftMap[(uint)code]) | elem.GetCodedIx(code);
                // Console.WriteLine("coded index = " + ix + " row = " + elem.Row);
                //} else {
                // Console.WriteLine("elem for coded index is null");
            }
            if (lgeCIx[(uint)code])
                output.Write(ix);
            else
                output.Write((ushort)ix);
        }

    }

}