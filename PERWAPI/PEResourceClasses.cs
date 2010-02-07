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
    public abstract class PEResourceElement
    {

        private int id;
        private string name;

        public PEResourceElement() { }

        public int Id
        {
            get { return id; }
            set { id = value; }
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        protected internal abstract uint Size();

        protected internal abstract void Write(BinaryWriter dest, uint RVA);
    }

    public class PEResourceDirectory : PEResourceElement
    {
        public PEResourceDirectory() { }

        private uint date = 0;
        private ushort majver = 1;
        private ushort minver = 0;

        public uint Date { get { return date; } set { date = value; } }
        public ushort MajVer { get { return majver; } set { majver = value; } }
        public ushort MinVer { get { return minver; } set { minver = value; } }

        private ArrayList subitems = new ArrayList();

        public PEResourceElement this[int i] { get { return (PEResourceElement)subitems[i]; } }

        public int Count() { return subitems.Count; }

        public bool HasData()
        {
            return subitems.Count > 0;
        }

        public void AddElement(PEResourceElement el)
        {
            subitems.Add(el);
        }
        private uint subsize, namesize, dirsize, numnamed;
        protected internal override uint Size()
        {
            namesize = 0;
            numnamed = 0;
            subsize = 0;
            for (int i = 0; i < subitems.Count; i++)
            {
                PEResourceElement el = (PEResourceElement)subitems[i];
                subsize += el.Size();
                if (el.Name != null)
                {
                    namesize += 2 + (uint)el.Name.Length * 2;
                    numnamed++;
                }
            }
            dirsize = (uint)subitems.Count * 8 + 16;
            return dirsize + namesize + subsize;
        }

        protected internal override void Write(BinaryWriter dest, uint RVA)
        {
            Size();
            dest.Flush();
            uint startnameoffset = (uint)dest.BaseStream.Position + (uint)dirsize;
            uint curritemoffset = startnameoffset + (uint)namesize;
            dest.Write((uint)0); // characteristics
            dest.Write(date); // datetime
            dest.Write(majver);
            dest.Write(minver);
            dest.Write((ushort)numnamed);
            dest.Write((ushort)(subitems.Count - numnamed));

            uint currnameoffset = startnameoffset;
            for (int i = 0; i < subitems.Count; i++)
            {
                PEResourceElement el = (PEResourceElement)subitems[i];
                if (el.Name != null)
                {
                    dest.Write((uint)(currnameoffset | 0x80000000));
                    if (el is PEResourceDirectory)
                        dest.Write((uint)(curritemoffset | 0x80000000));
                    else
                        dest.Write((uint)curritemoffset);
                    currnameoffset += 2 + (uint)el.Name.Length * 2;
                }
                curritemoffset += el.Size();
            }
            curritemoffset = startnameoffset + namesize;
            for (int i = 0; i < subitems.Count; i++)
            {
                PEResourceElement el = (PEResourceElement)subitems[i];
                if (el.Name == null)
                {
                    dest.Write(el.Id);
                    if (el is PEResourceDirectory)
                        dest.Write((uint)(curritemoffset | 0x80000000));
                    else
                        dest.Write((uint)curritemoffset);
                }
                curritemoffset += el.Size();
            }
            for (int i = 0; i < subitems.Count; i++)
            {
                PEResourceElement el = (PEResourceElement)subitems[i];
                string s = el.Name;
                if (s != null)
                {
                    dest.Write((ushort)s.Length);
                    byte[] b = System.Text.Encoding.Unicode.GetBytes(s);
                    dest.Write(b);
                }
            }
            for (int i = 0; i < subitems.Count; i++)
            {
                PEResourceElement el = (PEResourceElement)subitems[i];
                el.Write(dest, RVA);
            }

        }
    }

    public class PEResourceData : PEResourceElement
    {
        public PEResourceData() { }
        int codepage = 0;
        byte[] data;

        public int CodePage { get { return codepage; } set { codepage = value; } }

        public byte[] Data { get { return data; } set { data = value; } }

        protected internal override uint Size()
        {
            return 16 + (uint)Data.Length;
        }

        protected internal override void Write(BinaryWriter dest, uint RVA)
        {
            dest.Flush();
            dest.Write((uint)(dest.BaseStream.Position + 16) + RVA);
            dest.Write((uint)data.Length);
            dest.Write((uint)codepage);
            dest.Write((uint)0);
            dest.Write(data);
        }
    }
}