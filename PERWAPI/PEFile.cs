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
    /**************************************************************************/
    /**************************************************************************/
    // Base Class for PE Files
    /**************************************************************************/
    /**************************************************************************/
    /**************************************************************************/
    /// <summary>
    /// Base class for the PEFile (starting point)
    /// </summary>
    public class PEFile : Module
    {
        private string outputDir, fileName;
        private Stream outStream;
        private Assembly thisAssembly;
        PEWriter output;
        MetaDataOut metaData;

        System.IO.FileStream unmanagedResources;   // Unmanaged resources read from a file.

        internal PEResourceDirectory unmanagedResourceRoot; // Unmanaged resources added programmatically.
        internal MetaDataTables metaDataTables;
        internal PEFileVersionInfo versionInfo;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new PE File with the name "fileName".  If "fileName" ends in ".dll" then
        /// the file is a dll, otherwise it is an exe file.  This PE File has no assembly.
        /// </summary>
        /// <param name="fileName">Name for the output file.</param>
        public PEFile(string fileName)
            : base(fileName)
        {
            //PrimitiveType.ClearAddedFlags();   // KJG 18-April-2005 - Now done in MetaDataOut
            this.fileName = fileName;
            metaData = new MetaDataOut();
            versionInfo = new PEFileVersionInfo();
            versionInfo.SetDefaults(fileName);
        }

        /// <summary>
        /// Create a new PE File with the name "fileName".  If "fileName" ends in ".dll" then
        /// the file is a dll, otherwise it is an exe file.  This file has an Assembly called
        /// "assemblyName".
        /// </summary>
        /// <param name="fileName">Name for the output file</param>
        /// <param name="assemblyName">Name of the assembly</param>
        public PEFile(string fileName, string assemblyName)
            : base(fileName)
        {
            //PrimitiveType.ClearAddedFlags();   // KJG 18-April-2005 - Now done in MetaDataOut
            this.fileName = fileName;
            thisAssembly = new Assembly(assemblyName, this);
            metaData = new MetaDataOut();
            versionInfo = new PEFileVersionInfo();
            versionInfo.SetDefaults(fileName);
        }

        /// <summary>
        /// Read a PE file and create all the data structures to represent it
        /// </summary>
        /// <param name="filename">The file name of the PE file</param>
        /// <returns>PEFile object representing "filename"</returns>
        public static PEFile ReadPEFile(string filename)
        {
            return PEReader.ReadPEFile(filename, false);
        }
        /// <summary>
        /// Read an existing PE File and return the exported interface 
        /// (ie. anything that was specified as public).  
        /// All the MetaData structures will be Refs.
        /// </summary>
        /// <param name="filename">The name of the pe file</param>
        /// <returns>The AssemblyRef or ModuleRef describing the exported interface of the specified file</returns>
        public static ReferenceScope ReadExportedInterface(string filename)
        {
            return PEReader.GetExportedInterface(filename);
        }

        public static PEFile ReadPublicClasses(string filename)
        {
            PEFile pefile = PEReader.ReadPEFile(filename, true);
            ArrayList newClasses = new ArrayList();
            for (int i = 0; i < pefile.classes.Count; i++)
            {
                ClassDef aClass = (ClassDef)pefile.classes[i];
                if (aClass.isPublic()) newClasses.Add(aClass);
            }
            pefile.classes = newClasses;
            return pefile;
        }

        /*---------------------------- public set and get methods ------------------------------*/

        /// <summary>
        /// Get the version of .NET for this PE file
        /// </summary>
        /// <returns>.NET version</returns>
        public NetVersion GetNetVersion()
        {
            return versionInfo.netVersion;
        }

        /// <summary>
        /// Set the .NET version for this PE file
        /// </summary>
        /// <param name="nVer">.NET version</param>
        public void SetNetVersion(NetVersion nVer)
        {
            versionInfo.netVersion = nVer;
            versionInfo.netVerString = MetaData.versions[(int)versionInfo.netVersion];
            if ((nVer == NetVersion.Whidbey40) || (nVer == NetVersion.Whidbey41))
            {
                versionInfo.tsMinVer = 1;
            }
            else
            {
                versionInfo.tsMinVer = 0;
            }
            if (nVer == NetVersion.Whidbey50)
            {
                versionInfo.tsMajVer = 2;
            }
            GenericParam.extraField = nVer <= NetVersion.Whidbey40;
            if (Diag.DiagOn && GenericParam.extraField)
                Console.WriteLine("Writing extra field for GenericParams");
        }

        /// <summary>
        /// Get the .NET version for this PE file
        /// </summary>
        /// <returns>string representing the .NET version</returns>
        public string GetNetVersionString()
        {
            return versionInfo.netVerString.Trim();
        }

        /// <summary>
        /// Make a descriptor for an external assembly to this PEFile (.assembly extern)
        /// </summary>
        /// <param name="assemName">the external assembly name</param>
        /// <returns>a descriptor for this external assembly</returns>
        public AssemblyRef MakeExternAssembly(string assemName)
        {
            if (assemName.CompareTo(MSCorLib.mscorlib.Name()) == 0) return MSCorLib.mscorlib;
            return new AssemblyRef(assemName);
        }

        /// <summary>
        /// Make a descriptor for an external module to this PEFile (.module extern)
        /// </summary>
        /// <param name="name">the external module name</param>
        /// <returns>a descriptor for this external module</returns>
        public ModuleRef MakeExternModule(string name)
        {
            return new ModuleRef(name);
        }

        /// <summary>
        /// Set the directory that the PE File will be written to.  
        /// The default is the current directory.
        /// </summary>
        /// <param name="outputDir">The directory to write the PE File to.</param>
        public void SetOutputDirectory(string outputDir)
        {
            this.outputDir = outputDir;
        }

        /// <summary>
        /// Direct PE File output to an existing stream, instead of creating
        /// a new file.
        /// </summary>
        /// <param name="output">The output stream</param>
        public void SetOutputStream(Stream output)
        {
            this.outStream = output;
        }

        /// <summary>
        /// Specify if this PEFile is a .dll or .exe
        /// </summary>
        public void SetIsDLL(bool isDll)
        {
            versionInfo.isDLL = isDll;
            if (isDll)
                versionInfo.characteristics = FileImage.dllCharacteristics;
            else
                versionInfo.characteristics = FileImage.exeCharacteristics;
        }

        /// <summary>
        /// Set the subsystem (.subsystem) (Default is Windows Console mode)
        /// </summary>
        /// <param name="subS">subsystem value</param>
        public void SetSubSystem(SubSystem subS)
        {
            versionInfo.subSystem = subS;
        }

        /// <summary>
        /// Set the flags (.corflags)
        /// </summary>
        /// <param name="flags">the flags value</param>
        public void SetCorFlags(CorFlags flags)
        {
            versionInfo.corFlags = flags;
        }

        public string GetFileName()
        {
            return fileName;
        }

        public void SetFileName(string filename)
        {
            this.fileName = filename;
        }

        /// <summary>
        /// Get a Meta Data Element from this PE file
        /// </summary>
        /// <param name="token">The meta data token for the required element</param>
        /// <returns>The meta data element denoted by token</returns>
        public MetaDataElement GetElement(uint token)
        {
            if (buffer != null)
                return buffer.GetTokenElement(token);
            if (metaDataTables != null)
                return metaDataTables.GetTokenElement(token);
            return null;
        }

        /// <summary>
        /// Add an unmanaged resource to this PEFile 
        /// </summary>
        public void AddUnmanagedResourceFile(string resFilename)
        {
            if (!System.IO.File.Exists(resFilename))
                throw (new FileNotFoundException("Unmanaged Resource File Not Found", resFilename));
            // unmanagedResources = System.IO.File.OpenRead(resFilename);
            throw new NotYetImplementedException("Unmanaged Resources from input files are not yet implemented");
        }

        /// <summary>
        /// Add a managed resource to this PEFile.  The resource will be embedded in this PE file. 
        /// </summary>
        /// <param name="resName">The name of the managed resource</param>
        /// <param name="resBytes">The managed resource</param>
        /// <param name="isPublic">Access for the resource</param>
        public void AddManagedResource(string resName, byte[] resBytes, bool isPublic)
        {
            resources.Add(new ManifestResource(this, resName, resBytes, isPublic));
        }

        /// <summary>
        /// Add a managed resource from another assembly.
        /// </summary>
        /// <param name="resName">The name of the resource</param>
        /// <param name="assem">The assembly where the resource is</param>
        /// <param name="isPublic">Access for the resource</param>
        public void AddExternalManagedResource(string resName, AssemblyRef assem, bool isPublic)
        {
            resources.Add(new ManifestResource(this, resName, assem, 0, isPublic));
        }

        /// <summary>
        /// Add a managed resource from another file in this assembly.
        /// </summary>
        /// <param name="resName">The name of the resource</param>
        /// <param name="assem">The assembly where the resource is</param>
        /// <param name="isPublic">Access for the resource</param>
        public void AddExternalManagedResource(string resName, ResourceFile resFile, uint offset, bool isPublic)
        {
            resources.Add(new ManifestResource(this, resName, resFile, offset, isPublic));
        }

        /// <summary>
        /// Add a managed resource from another module in this assembly.
        /// </summary>
        /// <param name="resName">The name of the resource</param>
        /// <param name="assem">The assembly where the resource is</param>
        /// <param name="isPublic">Access for the resource</param>
        public void AddExternalManagedResource(string resName, ModuleRef mod, uint offset, bool isPublic)
        {
            resources.Add(new ManifestResource(this, resName, mod.modFile, offset, isPublic));
        }

        /// <summary>
        /// Add a managed resource from another assembly.
        /// </summary>
        /// <param name="mr"></param>
        /// <param name="isPublic"></param>
        public void AddExternalManagedResource(ManifestResource mr, bool isPublic)
        {
            resources.Add(new ManifestResource(this, mr, isPublic));
        }

        /// <summary>
        /// Find a resource
        /// </summary>
        /// <param name="name">The name of the resource</param>
        /// <returns>The resource with the name "name" or null </returns>
        public ManifestResource GetResource(string name)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                if (((ManifestResource)resources[i]).Name == name)
                    return (ManifestResource)resources[i];
            }
            return null;
        }

        public ManifestResource[] GetResources()
        {
            return (ManifestResource[])resources.ToArray(typeof(ManifestResource));
        }

        /// <summary>
        /// Get the descriptor for this assembly.  The PEFile must have been
        /// created with hasAssembly = true
        /// </summary>
        /// <returns>the descriptor for this assembly</returns>
        public Assembly GetThisAssembly()
        {
            return thisAssembly;
        }

        public AssemblyRef[] GetImportedAssemblies()
        {
            return buffer.GetAssemblyRefs();
        }

        public string[] GetNamesOfImports()
        {
            return buffer.GetAssemblyRefNames();
        }

        /*------------------------------------------ Output Methods -------------------------------*/

        private void BuildMetaData()
        {
            BuildMDTables(metaData);
            metaData.BuildCode();
            if (thisAssembly != null)
            {
                thisAssembly.BuildMDTables(metaData);
            }
            metaData.BuildMDTables(); // DoCustomAttributes, BuildSignatures for each in metadata tables
        }

        /// <summary>
        /// Write out the PEFile (the "bake" function)
        /// </summary>
        /// <param name="debug">include debug information</param>
        public void WritePEFile(bool writePDB)
        {
            if (outStream == null)
            {
                if (outputDir != null)
                {
                    if (!outputDir.EndsWith("\\"))
                        fileName = outputDir + "\\" + fileName;
                    else
                        fileName = outputDir + fileName;
                }
                output = new PEWriter(versionInfo, fileName, metaData, writePDB);
            }
            else
            {
                // Check to make sure we have not been asked to write a PDB
                if (writePDB) throw new Exception("You can not write PDB data when writing to a stream.  Please try writing to a file instead.");

                output = new PEWriter(versionInfo, outStream, metaData);
            }

            BuildMetaData();

            // If the application is roundtripping an input PE-file with
            // unmanaged resources, then this.unmanagedResourceRoot != null.
            if (this.unmanagedResourceRoot != null)
              output.AddUnmanagedResourceDirectory(this.unmanagedResourceRoot);

            output.MakeFile(versionInfo);
        }

        /// <summary>
        /// Makes the assembly debuggable by attaching the DebuggableAttribute
        /// to the Assembly. Call immediately before calling WritePEFile.
        /// </summary>
        /// <param name="allowDebug">set true to enable debugging, false otherwise</param>
        /// <param name="suppressOpt">set true to disable optimizations that affect debugging</param>
        public void MakeDebuggable(bool allowDebug, bool suppressOpt)
        {
            ClassRef debugRef = null;
            MethodRef dCtor = null;
            Type[] twoBools = new Type[] { PrimitiveType.Boolean, PrimitiveType.Boolean };
            debugRef = MSCorLib.mscorlib.GetClass("System.Diagnostics", "DebuggableAttribute");
            if (debugRef == null)
                debugRef = MSCorLib.mscorlib.AddClass("System.Diagnostics", "DebuggableAttribute");
            dCtor = debugRef.GetMethod(".ctor", twoBools);
            if (dCtor == null)
            {
                dCtor = debugRef.AddMethod(".ctor", PrimitiveType.Void, twoBools);
                dCtor.AddCallConv(CallConv.Instance);
            }
            Constant[] dbgArgs = new Constant[] { new BoolConst(allowDebug), new BoolConst(suppressOpt) };
            thisAssembly.AddCustomAttribute(dCtor, dbgArgs);
        }

        /// <summary>
        /// Write out a CIL text file for this PE file
        /// </summary>
        /// <param name="debug">include debug information</param>
        public void WriteCILFile(bool debug)
        {
            string cilFile = fileName.Substring(0, fileName.IndexOf('.')) + ".il";
            CILWriter writer = new CILWriter(cilFile, debug, this);
            writer.BuildCILInfo();
            writer.WriteFile(debug);
        }

        internal void SetThisAssembly(Assembly assem)
        {
            if (Diag.DiagOn) Console.WriteLine("Setting fileScope to assembly " + assem.Name());
            thisAssembly = assem;
        }

        internal void AddToResourceList(ManifestResource res)
        {
            resources.Add(res);
        }

        internal void AddToFileList()
        {
        }

        internal void SetDLLFlags(ushort dflags)
        {
            versionInfo.DLLFlags = dflags;
        }

        public void ReadPDB()
        {
            PDBReader reader = new PDBReader(this.fileName);
            foreach (ClassDef cDef in GetClasses())
                foreach (MethodDef mDef in cDef.GetMethods())
                {
                    CILInstructions buffer = mDef.GetCodeBuffer();
                    PDBMethod meth = reader.GetMethod((int)mDef.Token());

                    if (meth == null)
                        continue; // no symbols for this method

                    PDBSequencePoint[] spList = meth.SequencePoints;

                    MergeBuffer mergeBuffer = new MergeBuffer(buffer.GetInstructions());

                    PDBScope outer = meth.Scope;
                    Scope root = ReadPDBScope(outer, mergeBuffer, null, mDef);
                    buffer.currentScope = root;
                    bool hasRootScope = mergeBuffer.hasRootScope();

                    if (!hasRootScope)
                        mergeBuffer.Add(new OpenScope(root), (uint)0);
                    foreach (PDBSequencePoint sp in spList)
                    {
                        PDBDocument doc = sp.Document;
                        mergeBuffer.Add(
                            new Line((uint)sp.Line, (uint)sp.Column, (uint)sp.EndLine, (uint)sp.EndColumn,
                            SourceFile.GetSourceFile(doc.URL, doc.Language, doc.LanguageVendor, doc.DocumentType)),
                            (uint)sp.Offset);
                    }
                    if (!hasRootScope)
                        mergeBuffer.Add(new CloseScope(root), (uint)outer.EndOffset);

                    buffer.SetInstructions(mergeBuffer.Instructions);
                }
        }

        private Scope ReadPDBScope(PDBScope scope, MergeBuffer mergeBuffer, Scope parent, MethodDef thisMeth)
        {
            Scope thisScope = new Scope(parent, thisMeth);

            if (parent != null) mergeBuffer.Add(new OpenScope(thisScope), (uint)scope.StartOffset);

            foreach (PDBVariable var in scope.Variables)
                thisScope.AddLocalBinding(var.Name, var.Address);

            foreach (PDBScope child in scope.Children)
                ReadPDBScope(child, mergeBuffer, thisScope, thisMeth);

            if (parent != null) mergeBuffer.Add(new CloseScope(thisScope), (uint)scope.EndOffset);

            return thisScope;
        }
    }

    /**************************************************************************/
    internal class PEFileVersionInfo
    {

        private static char[] nulls = { '\0' };
        internal bool fromExisting;
        internal ushort characteristics;
        internal bool isDLL;
        internal byte lMajor;
        internal byte lMinor;
        internal uint fileAlign;
        internal ushort osMajor;
        internal ushort osMinor;
        internal ushort userMajor;
        internal ushort userMinor;
        internal ushort subSysMajor;
        internal ushort subSysMinor;
        internal SubSystem subSystem;
        internal ushort DLLFlags = 0;
        internal ushort cliMajVer;
        internal ushort cliMinVer;
        internal CorFlags corFlags = CorFlags.CF_IL_ONLY;
        internal ushort mdMajVer;
        internal ushort mdMinVer;
        internal NetVersion netVersion;
        internal string netVerString;
        internal byte tsMajVer;
        internal byte tsMinVer;

        internal void SetDefaults(string name)
        {
            fromExisting = false;
            isDLL = name.EndsWith(".dll") || name.EndsWith(".DLL");
            if (isDLL)
            {
                characteristics = FileImage.dllCharacteristics;
            }
            else
            {
                characteristics = FileImage.exeCharacteristics;
            }
            lMajor = MetaData.LMajors[0];
            lMinor = 0;
            fileAlign = FileImage.minFileAlign;
            osMajor = 4;
            osMinor = 0;
            userMajor = 0;
            userMinor = 0;
            subSysMajor = 4;
            subSysMinor = 0;
            subSystem = SubSystem.Windows_CUI;
            DLLFlags = FileImage.DLLFlags;
            cliMajVer = 2;
            cliMinVer = 0;
            corFlags = CorFlags.CF_IL_ONLY;
            mdMajVer = 1;
            mdMinVer = 1; // MetaData Minor Version  ECMA = 0, PEFiles = 1
            netVersion = NetVersion.Everett;
            netVerString = MetaData.versions[0];
            tsMajVer = 1;
            tsMinVer = 0;
        }

        internal void SetVersionFromString()
        {
            for (int i = 0; i < MetaData.versions.Length; i++)
            {
                if (MetaData.versions[i].Trim(nulls) == netVerString)
                {
                    netVersion = (NetVersion)i;
                    netVerString = MetaData.versions[i];
                }
            }
        }

    }


}