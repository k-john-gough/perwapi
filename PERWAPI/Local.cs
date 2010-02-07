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
    // Class to describe procedure locals
    /**************************************************************************/
    /// <summary>
    /// Descriptor for a local of a method
    /// </summary>
    public class Local
    {
        private static readonly byte PINNED = 0x45;
        string name;
        public Type type;
        bool pinned = false;
        int index = 0;

        /*-------------------- Constructors ---------------------------------*/

        /// <summary>
        /// Create a new local variable 
        /// </summary>
        /// <param name="lName">name of the local variable</param>
        /// <param name="lType">type of the local variable</param>
        public Local(string lName, Type lType)
        {
            name = lName;
            type = lType;
        }

        /// <summary>
        /// Create a new local variable that is byref and/or pinned
        /// </summary>
        /// <param name="lName">local name</param>
        /// <param name="lType">local type</param>
        /// <param name="isPinned">has pinned attribute</param>
        public Local(string lName, Type lType, bool isPinned)
        {
            name = lName;
            type = lType;
            pinned = isPinned;
        }

        public int GetIndex() { return index; }

        /// <summary>
        /// The name of the local variable.
        /// </summary>
        public string Name { get { return name; } }

        public bool Pinned
        {
            get { return pinned; }
            set { pinned = value; }
        }

        /// <summary>
        /// Gets the signature for this local variable.
        /// </summary>
        /// <returns>A byte array of the signature.</returns>
        public byte[] GetSig()
        {
            MemoryStream str = new MemoryStream();
            type.TypeSig(str);
            return str.ToArray();
        }

        internal void SetIndex(int ix)
        {
            index = ix;
        }

        internal void TypeSig(MemoryStream str)
        {
            if (pinned) str.WriteByte(PINNED);
            type.TypeSig(str);
        }

        internal void BuildTables(MetaDataOut md)
        {
            if (!(type is ClassDef))
                type.BuildMDTables(md);
        }

        internal void BuildCILInfo(CILWriter output)
        {
            if (!(type is ClassDef))
                type.BuildCILInfo(output);
        }

        internal void Write(CILWriter output)
        {
            type.WriteType(output);
            output.Write("\t" + name);
        }

    }
}
