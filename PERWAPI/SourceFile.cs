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
    /// <summary>
    /// Descriptor for a file containing a managed resource
    /// </summary>
    public class SourceFile
    {
        static ArrayList sourceFiles = new ArrayList();
        internal string name;
        internal Guid language, vendor, document;

        /*-------------------- Constructors ---------------------------------*/

        private SourceFile(string name, Guid lang, Guid vend, Guid docu)
        {
            this.name = name;
            language = lang;
            vendor = vend;
            document = docu;
            sourceFiles.Add(this);
        }

        private bool GuidsMatch(Guid lang, Guid vend, Guid docu)
        {
            if (language != lang) return false;
            if (vendor != vend) return false;
            if (document != docu) return false;
            return true;
        }

        internal bool Match(SourceFile file)
        {
            if (file == null) return false;
            if (this == file) return true;
            if (name != file.name) return false;
            return GuidsMatch(file.language, file.vendor, file.document);
        }

        public static SourceFile GetSourceFile(string name, Guid lang, Guid vend, Guid docu)
        {
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                SourceFile sFile = (SourceFile)sourceFiles[i];
                if ((sFile.name == name) && sFile.GuidsMatch(lang, vend, docu))
                    return sFile;
            }
            return new SourceFile(name, lang, vend, docu);
        }

        public string Name
        {
            get { return name; }
        }

    }

}