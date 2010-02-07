using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics.SymbolStore;

namespace QUT.Symbols {
  #region HelperInterfaces
  /// <summary>
  /// This is really just a hook to hang the GUID on,
  /// so that we can pass "typeof(IMetadataImport)" 
  /// to Marshal.GetComInterfaceForObject()
  /// </summary>
  [
  ComVisible(true),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("7DAC8207-D3AE-4c75-9B67-92801A497D44")
  ]
  public interface IMetadataImport {
    void Dummy();
  }

  /// <summary>
  /// Helper interface for the managed reader.
  /// This is based on the interface of the same name
  /// defined in the C++ header file corsym.h
  /// </summary>
  [
      ComImport,
      ComVisible(false),
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
      Guid("B62B923C-B500-3158-A543-24F307A8B7E1")
  ]
  interface ISymUnmanagedMethod {
    void GetToken(out SymbolToken pToken);

    void GetSequencePointCount(out int retVal); // used by PERWAPI

    void GetRootScope( // used by PERWAPI
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope retVal);

    void GetScopeFromOffset(
        int offset,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope retVal);

    void GetOffset(
        ISymUnmanagedDocument document,
        int line,
        int column,
        out int retVal);

    void GetRanges(
        ISymUnmanagedDocument document,
        int line,
        int column,
        int cRanges,
        out int pcRanges,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] int[] ranges);

    void GetParameters(
        int cParams,
        out int pcParams,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedVariable[] parms);

    void GetNamespace(
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedNamespace retVal);

    void GetSourceStartEnd(
        ISymUnmanagedDocument[] docs,
        [In, Out, MarshalAs(UnmanagedType.LPArray)] int[] lines,
        [In, Out, MarshalAs(UnmanagedType.LPArray)] int[] columns,
        out Boolean retVal);

    void GetSequencePoints( // used by PERWAPI
        int cPoints,
        out int pcPoints,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] offsets,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedDocument[] documents,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] lines,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] columns,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] endLines,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] endColumns);
  }


  /// <summary>
  /// This definition is a hook for the COM interface.
  /// We use [PreserveSig] to call the bare function
  /// returning an HRESULT.  We only need GetReaderForFile()
  /// which is in the first slot of the vtable.
  /// 
  /// The definition is in C++ header corsym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("AA544d42-28CB-11d3-bd22-0000f80849bd")
  ]
  internal interface ISymUnmanagedBinder {
    [PreserveSig]
    int GetReaderForFile(
        IntPtr importer,
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        [MarshalAs(UnmanagedType.LPWStr)] string dummyPath,
        [MarshalAs(UnmanagedType.Interface)] out object retVal);
  }

  /// <summary>
  /// This interface is a minimal cover of  the interface
  /// of the same name defined in C++ header corsym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("B4CE6286-2A6B-3712-A3B7-1EE1DAD467B5")
  ]
  internal interface ISymUnmanagedReader {
    void GetDocument(
        [MarshalAs(UnmanagedType.LPWStr)] String url,
        Guid language,
        Guid languageVendor,
        Guid documentType,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedDocument retVal);

    void GetDocuments(
        int cDocs,
        out int pcDocs,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedDocument[] pDocs);

    [PreserveSig]
    int GetUserEntryPoint(out SymbolToken EntryPoint);

    [PreserveSig]
    int GetMethod(
        SymbolToken methodToken,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod retVal);

    [PreserveSig]
    int GetMethodByVersion(
        SymbolToken methodToken,
        int version,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod retVal);

    void GetVariables(
        SymbolToken parent,
        int cVars,
        out int pcVars,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ISymUnmanagedVariable[] vars);

    void GetGlobalVariablesDummy();

    void GetMethodFromDocumentPosition(
        ISymUnmanagedDocument document,
        int line,
        int column,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod retVal);

    void GetSymAttributeDummy();
    void GetNamespacesDummy();

    void Initialize(
        IntPtr importer,
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        [MarshalAs(UnmanagedType.LPWStr)] string searchPath,
        IStream stream);

    void UpdateSymbolStoreDummy();
    void ReplaceSymbolStoreDummy();
    void GetSymbolStoreFileNameDummy();
    void GetMethodsFromDocumentPositionDummy();
    void GetDocumentVersionDummy();
    void GetMethodVersionDummy();
  }

  /// <summary>
  /// A minimal subset of the COM IMetaDataDispenser interface
  /// </summary>
  [
  ComVisible(true),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")
  ]
  internal interface IMetaDataDispenserSubset {
    void DefineScope_Dummy(); // Put here to index vtable correctly

    void OpenScope(
        [In, MarshalAs(UnmanagedType.LPWStr)] string szScope,
        [In] int dwOpenFlags,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object pUnk);
  }


  /// <summary>
  /// This interface is defined in C++ header corsym.h
  /// </summary>
  [
      ComImport,
      ComVisible(false),
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
      Guid("9F60EEBE-2D9A-3F7C-BF58-80BC991C60BB")
 ]
  internal interface ISymUnmanagedVariable {
    void GetName(
        int cchName,
        out int pcchName,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

    void GetAttributes(out int pRetVal);

    void GetSignature(
        int cSig,
        out int pcSig,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] sig);

    void GetAddressKind(out int pRetVal);

    void GetAddressField1(out int pRetVal);

    void GetAddressField2(out int pRetVal);

    void GetAddressField3(out int pRetVal);

    void GetStartOffset(out int pRetVal);

    void GetEndOffset(out int pRetVal);
  }

  /// <summary>
  /// This interface defined in C++ header file CorSym.h
  /// </summary>
  [
      ComImport,
      ComVisible(false),
      InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
      Guid("40DE4037-7C81-3E1E-B022-AE1ABFF2CA08"),
 ]
  internal interface ISymUnmanagedDocument // needed by PERWAPI
  {
    void GetURL(
        int cchUrl,
        out int pcchUrl,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szUrl);

    void GetDocumentType(ref Guid pRetVal);

    void GetLanguage(ref Guid pRetVal);

    void GetLanguageVendor(ref Guid pRetVal);

    void GetCheckSumAlgorithmId(ref Guid pRetVal);

    void GetCheckSum(
        int cData,
        out int pcData,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] data);

    void FindClosestLine(
        int line,
        out int pRetVal);

    void HasEmbeddedSource(out Boolean pRetVal);

    void GetSourceLength(out int pRetVal);

    void GetSourceRange(
        int startLine,
        int startColumn,
        int endLine,
        int endColumn,
        int cSourceBytes,
        out int pcSourceBytes,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] source);

  };

  // =====================================================================================

  /// <summary>
  /// This interface defined in C++ header file CorSym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("68005D0F-B8E0-3B01-84D5-A11A94154942")
  ]
  internal interface ISymUnmanagedScope // needed by PERWAPI
  {
    void GetMethod(
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod pRetVal);

    void GetParent(
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedScope pRetVal);

    void GetChildren(
        int cChildren,
        out int pcChildren,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedScope[] children);

    void GetStartOffset(out int pRetVal);

    void GetEndOffset(out int pRetVal);

    void GetLocalCount(out int pRetVal);

    void GetLocals(
        int cLocals,
        out int pcLocals,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedVariable[] locals);

    void GetNamespaces(
        int cNameSpaces,
        out int pcNameSpaces,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedNamespace[] namespaces);
  };


  /// <summary>
  /// This interface defined in C++ header file CorSym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("0DFF7289-54F8-11d3-BD28-0000F80849BD")
  ]
  internal interface ISymUnmanagedNamespace {
    void GetName(
        int cchName,
        out int pcchName,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

    void GetNamespaces(
        int cNameSpaces,
        out int pcNameSpaces,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedNamespace[] namespaces);

    void GetVariables(
        int cVars,
        out int pcVars,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ISymUnmanagedVariable[] pVars);
  }

  #endregion // Helper Interfaces

  #region SymbolWriterExtras

  /// <summary>
  /// This interface is a minimal cover of  the interface
  /// of the same name defined in C++ header corsym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
  Guid("0B97726E-9E6D-4f05-9A26-424022093CAA")
  ]
  internal interface ISymUnmanagedWriter2 {
    void DefineDocument( // used
        [MarshalAs(UnmanagedType.LPWStr)] string url,
        ref Guid language,
        ref Guid languageVendor,
        ref Guid documentType,
        [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedDocumentWriter RetVal);

    void SetUserEntryPoint(SymbolToken entryMethod); // used

    void OpenMethod(SymbolToken method); // used

    void CloseMethod(); // used

    void OpenScope(int startOffset, out int pRetVal); // used

    void CloseScope(int endOffset); // used

    void SetScopeRange(int scopeID, int startOffset, int endOffset);

    void DefineLocalVariable(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes, int cSig,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] signature,
        int addressKind,
        int addr1,
        int addr2,
        int addr3,
        int startOffset,
        int endOffset);

    void DefineParameter(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes,
        int sequence,
        int addressKind,
        int addr1,
        int addr2,
        int addr3);

    void DefineField(
        SymbolToken parent,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes, int cSig,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] signature,
        int addressKind,
        int addr1,
        int addr2,
        int addr3);

    void DefineGlobalVariable(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes, int cSig,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] signature,
        int addressKind,
        int addr1,
        int addr2,
        int addr3);

    void Close(); // used

    void SetSymAttribute(
        SymbolToken parent,
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int cData,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] data);

    void OpenNamespace([MarshalAs(UnmanagedType.LPWStr)] string name);

    void CloseNamespace();

    void UsingNamespace([MarshalAs(UnmanagedType.LPWStr)] string fullName);

    void SetMethodSourceRange(
        ISymUnmanagedDocumentWriter startDoc,
        int startLine, int startColumn,
        ISymUnmanagedDocumentWriter endDoc,
        int endLine,
        int endColumn);

    void Initialize( // used
        IntPtr emitter,
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        IStream stream,
        bool fullBuild);

    void GetDebugInfo( // used
        out ImageDebugDirectory iDD,
        int cData,
        out int pcData,
        [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] data);

    void DefineSequencePoints( // used
        ISymUnmanagedDocumentWriter document,
        int spCount,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] offsets,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] lines,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] columns,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] endLines,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] int[] endColumns);

    void RemapToken(SymbolToken oldToken, SymbolToken newToken);

    void Initialize2(
        IntPtr emitter,
        [MarshalAs(UnmanagedType.LPWStr)] string tempfilename,
        IStream stream,
        bool fullBuild, [
        MarshalAs(UnmanagedType.LPWStr)] string finalfilename);

    void DefineConstant(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        object value,
        int cSig,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] signature);

    void Abort();

    void DefineLocalVariable2( // used
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes,
        SymbolToken sigToken,
        int addressKind,
        int addr1,
        int addr2,
        int addr3,
        int startOffset,
        int endOffset);

    void DefineGlobalVariable2(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        int attributes,
        SymbolToken sigToken,
        int addressKind,
        int addr1,
        int addr2,
        int addr3);

    void DefineConstant2(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        object value,
        SymbolToken sigToken);
  }

  /// <summary>
  /// This interface is a minimal cover of  the interface
  /// of the same name defined in C++ header corsym.h
  /// </summary>
  [
  ComImport,
  ComVisible(false),
  Guid("B01FAFEB-C450-3A4D-BEEC-B4CEEC01E006"),
  InterfaceType(ComInterfaceType.InterfaceIsIUnknown)
  ]
  internal interface ISymUnmanagedDocumentWriter {
    void SetSource(
        int sourceSize,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] source);

    void SetCheckSum(
        Guid algorithmId,
        int checkSumSize,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] checkSum);
  }

  #endregion // SymbolWriterExtras
}
