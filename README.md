# Project Description
PERWAPI is a reader writer module for .NET program executables.  It has been used as back-end for progamming language compilers such as Gardens Point Component Pascal, the Ruby.NET prototype and .NET assemblers. 
PERWAPI is written in C# and optionally produces PDB files also.


_PERWAPI_ is a module that reads and writes .NET program executable files. It was developed primarily as a file reader writer for programming language compilers.  It defines classes for all of the features of the IL model, and methods to read and write the metadata of the PE-file.  The module is written in C#, and does not rely on any facilities outside the base class libraries.  It uses neither unmanaged code nor COM interop.

In its current form it supports most of the features of the .NET V3.5 framework.

A second module of the project _SymbolRW_ provides a managed interface to the COM interface for reading and writing PDB debug files.  This module is a minimal interface for the features typically required for debug information of compilers.  It does not provide access to all of the features of mscoree.dll.
