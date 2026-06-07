// Required to use `record` and `init` setters on netstandard2.0.
// The C# compiler emits references to this type; we provide it ourselves.

using System.ComponentModel;

// MUST be in this exact namespace: the C# compiler emits references to
// System.Runtime.CompilerServices.IsExternalInit for `init` setters / records.
// Do NOT let an IDE "adjust namespaces to match folder" rewrite this.
// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
