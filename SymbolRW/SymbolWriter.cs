//
//  This code is the managed pdb writer interface for PERWAPI.
//  Written by John Gough, January 2008. 
//  Copyright(c) 2007-2010 John Gough, and Queensland University of Technology
//
//  NOTES:
//  Q: Where does this code come from?
//  A: At the heart of the code is some COM interop that accesses the
//     facilities of mscoree.dll.  Unfortunately, the type library for
//     this component is incomplete, and does not cover the symbol store
//     functionality.  There is some MIDL, and the C++ header files
//     cor.h and corsym.h.  From here comes the guid values, for 
//     example, and critically the order of the interface methods.
//     The managed interfaces, such as ISymbolReader, are defined in 
//     [mscorlib]System.Diagnostics.SymbolStore.
//
//     This file provides managed wrappers for the unmanaged interfaces.
//     These wrappers implement the managed interfaces. 
//     Not all methods are implemented. Those that are not will throw 
//     NotImplementedException.
//
//     Some of the functionality currently not used by PERWAPI is left
//     unimplemented, and throws a NotImplementedException if called.
//
//  THANKS:
//  Wayne Kelly, whose SimpleWriter, in C++ pointed the way.
//  Adam Nathan, whose ".NET and COM" tells you more than you want to know
//  Microsoft's Iron Python and MDBG samples answered remaining questions
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics.SymbolStore;

namespace QUT.Symbols {
  public class SymbolWriter {
    private ISymUnmanagedWriter2 writer;

    public SymbolWriter(string binaryFile, string pdbFile) {
      object dispenser = null;
      object pUnknown = null;
      IntPtr importer = IntPtr.Zero;
      object writer2 = null;

      try {
        OLE32.CoCreateInstance(ref XGuid.dispenserClassID, null, 1, ref XGuid.dispenserIID, out dispenser);
        ((IMetaDataDispenserSubset)dispenser).OpenScope(binaryFile, 0, ref XGuid.importerIID, out pUnknown);
        importer = Marshal.GetComInterfaceForObject(pUnknown, typeof(IMetadataImport));

        OLE32.CoCreateInstance(ref XGuid.symWriterClassID, null, 1, ref XGuid.symWriterIID, out writer2);
        writer = (ISymUnmanagedWriter2)writer2;
        writer.Initialize(importer, pdbFile, null, true);
      }
      catch (Exception x) {
        Console.WriteLine(x.Message);
      }
      finally {
        if (importer != IntPtr.Zero)
          Marshal.Release(importer);
      }
    }

    public object DefineDocument(string url, ref Guid language, ref Guid vendor, ref Guid docType) {
      ISymUnmanagedDocumentWriter docWriter;
      writer.DefineDocument(url, ref language, ref vendor, ref docType, out docWriter);
      return (object)docWriter;
    }

    public void SetUserEntryPoint(SymbolToken tok) {
      writer.SetUserEntryPoint(tok);
    }

    public void DefineSequencePoints(
        object doc,
        int[] offsets, int[] lines, int[] cols, int[] endLines, int[] endCols) {
      ISymUnmanagedDocumentWriter pDoc = (ISymUnmanagedDocumentWriter)doc;
      writer.DefineSequencePoints(pDoc, offsets.Length, offsets, lines, cols, endLines, endCols);
    }

    public void DefineLocalVariable2(
        string name,
        int attr, SymbolToken tok, int addrKind, int addr1, int addr2, int addr3, int startOfst, int endOfst) {
      writer.DefineLocalVariable2(name, attr, tok, addrKind, addr1, addr2, addr3, startOfst, endOfst);
    }

    public int OpenScope(int startOffset) {
      int rslt;
      writer.OpenScope(startOffset, out rslt);
      return rslt;
    }

    public void CloseScope(int endOffset) {
      writer.CloseScope(endOffset);
    }

    public void OpenMethod(SymbolToken tok) {
      writer.OpenMethod(tok);
    }

    public void CloseMethod() {
      writer.CloseMethod();
    }

    public byte[] GetDebugInfo() {
      int length;
      byte[] info = null;
      ImageDebugDirectory idd;

      writer.GetDebugInfo(out idd, 0, out length, null);
      info = new byte[length];
      writer.GetDebugInfo(out idd, length, out length, info);
      return info;
    }

    public void Close() {
      writer.Close();
    }
  }
}
