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
    // Class containing MetaData Constants
    /**************************************************************************/
    /// <summary>
    /// MetaData 
    ///   Root (20 bytes + UTF-8 Version String + quad align padding)
    ///   StreamHeaders (8 bytes + null terminated name string + quad align padding)
    ///   Streams 
    ///     #~        (always present - holds metadata tables)
    ///     #Strings  (always present - holds identifier strings)
    ///     #US       (Userstring heap)
    ///     #Blob     (signature blobs)
    ///     #GUID     (guids for assemblies or Modules)
    /// </summary>

    internal class MetaData
    {
        internal static readonly uint maxSmlIxSize = 0xFFFF;
        internal static readonly uint max1BitSmlIx = 0x7FFF;
        internal static readonly uint max2BitSmlIx = 0x3FFF;
        internal static readonly uint max3BitSmlIx = 0x1FFF;
        internal static readonly uint max5BitSmlIx = 0x7FF;
        internal static readonly uint[] CIxBitMasks = { 0x0, 0x0001, 0x0003, 0x0007, 0x000F, 0x001F };
        internal static readonly int[] CIxShiftMap = { 2, 2, 5, 1, 2, 3, 1, 1, 1, 2, 3, 2, 1 };
        internal static readonly uint[] CIxMaxMap = {max2BitSmlIx,max2BitSmlIx,max5BitSmlIx,
                                                        max1BitSmlIx,max2BitSmlIx,max3BitSmlIx,
                                                        max1BitSmlIx,max1BitSmlIx,max1BitSmlIx,
                                                        max2BitSmlIx,max3BitSmlIx,max2BitSmlIx, max1BitSmlIx};
        internal static readonly int[] TypeDefOrRefTable = { (int)MDTable.TypeDef, (int)MDTable.TypeRef, (int)MDTable.TypeSpec };
        internal static readonly int[] HasConstantTable = { (int)MDTable.Field, (int)MDTable.Param, (int)MDTable.Property };
        internal static readonly int[] HasCustomAttributeTable = {(int)MDTable.Method, (int)MDTable.Field, (int)MDTable.TypeRef, 
                                                                     (int)MDTable.TypeDef,(int)MDTable.Param, (int)MDTable.InterfaceImpl,
                                                                     (int)MDTable.MemberRef, (int)MDTable.Module,(int)MDTable.DeclSecurity,
                                                                     (int)MDTable.Property,(int)MDTable.Event, (int)MDTable.StandAloneSig, 
                                                                     (int)MDTable.ModuleRef, (int)MDTable.TypeSpec, (int)MDTable.Assembly,
                                                                     (int)MDTable.AssemblyRef, (int)MDTable.File, (int)MDTable.ExportedType, 
                                                                     (int)MDTable.ManifestResource };
        internal static readonly int[] HasFieldMarshalTable = { (int)MDTable.Field, (int)MDTable.Param };
        internal static readonly int[] HasDeclSecurityTable = { (int)MDTable.TypeDef, (int)MDTable.Method, (int)MDTable.Assembly };
        internal static readonly int[] MemberRefParentTable = {(int)MDTable.TypeDef, (int)MDTable.TypeRef, (int)MDTable.ModuleRef, (int)MDTable.Method, 
                                                                  (int)MDTable.TypeSpec };
        internal static readonly int[] HasSemanticsTable = { (int)MDTable.Event, (int)MDTable.Property };
        internal static readonly int[] MethodDefOrRefTable = { (int)MDTable.Method, (int)MDTable.MemberRef };
        internal static readonly int[] MemberForwardedTable = { (int)MDTable.Field, (int)MDTable.Method };
        internal static readonly int[] ImplementationTable = { (int)MDTable.File, (int)MDTable.AssemblyRef, (int)MDTable.ExportedType };
        internal static readonly int[] CustomAttributeTypeTable = { 0, 0, (int)MDTable.Method, (int)MDTable.MemberRef };
        internal static readonly int[] ResolutionScopeTable = {(int)MDTable.Module, (int)MDTable.ModuleRef, (int)MDTable.AssemblyRef,
                                                                  (int)MDTable.TypeRef };
        internal static readonly int[] TypeOrMethodDefTable = { (int)MDTable.TypeDef, (int)MDTable.Method };
        internal static readonly int[][] CIxTables = {TypeDefOrRefTable, HasConstantTable,
                                                         HasCustomAttributeTable, HasFieldMarshalTable, HasDeclSecurityTable, 
                                                         MemberRefParentTable, HasSemanticsTable, MethodDefOrRefTable, MemberForwardedTable,
                                                         ImplementationTable, CustomAttributeTypeTable, ResolutionScopeTable,
                                                         TypeOrMethodDefTable };

        internal static readonly byte StringsHeapMask = 0x1;
        internal static readonly byte GUIDHeapMask = 0x2;
        internal static readonly byte BlobHeapMask = 0x4;
        internal static readonly uint MetaDataSignature = 0x424A5342;
        // NOTE: version and stream name strings MUST always be quad padded
        internal static readonly string[] versions = {  "v1.1.4322\0\0\0", 
                                                        "v2.0.40607\0\0", 
                                                        "v2.0.41202\0\0",
                                                        "v2.0.50215\0\0",
                                                        "v2.0.50727\0\0",
                                                        "2.0.0.0\0"
                                                     };
        internal static readonly byte[] LMajors = { 6, 8, 8, 8, 8 };
        //internal static readonly string shortVersion = version.Substring(0,9);
        internal static readonly char[] tildeNameArray = { '#', '~', '\0', '\0' };
        internal static readonly char[] stringsNameArray = { '#', 'S', 't', 'r', 'i', 'n', 'g', 's', '\0', '\0', '\0', '\0' };
        internal static readonly char[] usNameArray = { '#', 'U', 'S', '\0' };
        internal static readonly char[] guidNameArray = { '#', 'G', 'U', 'I', 'D', '\0', '\0', '\0' };
        internal static readonly char[] blobNameArray = { '#', 'B', 'l', 'o', 'b', '\0', '\0', '\0' };
        internal static readonly String stringsName = "#Strings";
        internal static readonly String userstringName = "#US";
        internal static readonly String blobName = "#Blob";
        internal static readonly String guidName = "#GUID";
        internal static readonly String tildeName = "#~";
        internal static readonly uint MetaDataHeaderSize = 20 + (uint)versions[0].Length;
        internal static readonly uint TildeHeaderSize = 24;
        internal static readonly uint StreamHeaderSize = 8;
        internal static readonly uint NumMetaDataTables = (int)MDTable.MaxMDTable;
        internal static readonly uint tildeHeaderSize = 8 + (uint)tildeNameArray.Length;

        internal ulong valid = 0, /*sorted = 0x000002003301FA00;*/ sorted = 0;
        internal bool[] largeIx = new bool[NumMetaDataTables];
        internal bool[] lgeCIx = new bool[(int)CIx.MaxCIx];
        internal uint[] elemSize = new uint[NumMetaDataTables];
        internal bool largeStrings = false, largeUS = false, largeGUID = false, largeBlob = false;

        internal MetaData()
        {
            InitMetaData();
        }

        internal void InitMetaData()
        {
            for (int i = 0; i < NumMetaDataTables; i++)
                largeIx[i] = false;
            for (int i = 0; i < lgeCIx.Length; i++)
                lgeCIx[i] = false;
        }

        internal bool LargeIx(MDTable tabIx) { return largeIx[(uint)tabIx]; }

        internal uint CodedIndexSize(CIx code)
        {
            if (lgeCIx[(uint)code]) return 4;
            return 2;
        }

        internal uint TableIndexSize(MDTable tabIx)
        {
            if (largeIx[(uint)tabIx]) return 4;
            return 2;
        }

        internal uint StringsIndexSize()
        {
            if (largeStrings) return 4;
            return 2;
        }

        internal uint GUIDIndexSize()
        {
            if (largeGUID) return 4;
            return 2;
        }

        internal uint USIndexSize()
        {
            if (largeUS) return 4;
            return 2;
        }

        internal uint BlobIndexSize()
        {
            if (largeBlob) return 4;
            return 2;
        }

        internal void CalcElemSize()
        {
            elemSize[(int)MDTable.Assembly] = Assembly.Size(this);
            elemSize[(int)MDTable.AssemblyOS] = 12;
            elemSize[(int)MDTable.AssemblyProcessor] = 4;
            elemSize[(int)MDTable.AssemblyRefOS] = 12 + TableIndexSize(MDTable.AssemblyRef);
            elemSize[(int)MDTable.AssemblyRefProcessor] = 4 + TableIndexSize(MDTable.AssemblyRef);
            elemSize[(int)MDTable.Module] = Module.Size(this);
            elemSize[(int)MDTable.TypeRef] = ClassRef.Size(this);
            elemSize[(int)MDTable.TypeDef] = ClassDef.Size(this);
            elemSize[(int)MDTable.Field] = FieldDef.Size(this);
            elemSize[(int)MDTable.Method] = MethodDef.Size(this);
            elemSize[(int)MDTable.Param] = Param.Size(this);
            elemSize[(int)MDTable.InterfaceImpl] = InterfaceImpl.Size(this);
            elemSize[(int)MDTable.MemberRef] = FieldRef.Size(this);
            elemSize[(int)MDTable.Constant] = ConstantElem.Size(this);
            elemSize[(int)MDTable.CustomAttribute] = CustomAttribute.Size(this);
            elemSize[(int)MDTable.FieldMarshal] = FieldMarshal.Size(this);
            elemSize[(int)MDTable.DeclSecurity] = DeclSecurity.Size(this);
            elemSize[(int)MDTable.ClassLayout] = ClassLayout.Size(this);
            elemSize[(int)MDTable.FieldLayout] = FieldLayout.Size(this);
            elemSize[(int)MDTable.StandAloneSig] = Signature.Size(this);
            elemSize[(int)MDTable.EventMap] = MapElem.Size(this, MDTable.EventMap);
            elemSize[(int)MDTable.Event] = Event.Size(this);
            elemSize[(int)MDTable.PropertyMap] = MapElem.Size(this, MDTable.PropertyMap);
            elemSize[(int)MDTable.Property] = Property.Size(this);
            elemSize[(int)MDTable.MethodSemantics] = MethodSemantics.Size(this);
            elemSize[(int)MDTable.MethodImpl] = MethodImpl.Size(this);
            elemSize[(int)MDTable.ModuleRef] = ModuleRef.Size(this);
            elemSize[(int)MDTable.TypeSpec] = TypeSpec.Size(this);
            elemSize[(int)MDTable.ImplMap] = ImplMap.Size(this);
            elemSize[(int)MDTable.FieldRVA] = FieldRVA.Size(this);
            elemSize[(int)MDTable.Assembly] = Assembly.Size(this);
            elemSize[(int)MDTable.AssemblyRef] = AssemblyRef.Size(this);
            elemSize[(int)MDTable.File] = FileRef.Size(this);
            elemSize[(int)MDTable.ExportedType] = ExternClass.Size(this);
            elemSize[(int)MDTable.ManifestResource] = ManifestResource.Size(this);
            elemSize[(int)MDTable.NestedClass] = MapElem.Size(this, MDTable.NestedClass);
            elemSize[(int)MDTable.GenericParam] = GenericParam.Size(this);
            elemSize[(int)MDTable.GenericParamConstraint] = GenericParamConstraint.Size(this);
            elemSize[(int)MDTable.MethodSpec] = MethodSpec.Size(this);
        }

    }

}
