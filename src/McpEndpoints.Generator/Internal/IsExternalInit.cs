// Required to use `record` and `init` setters on netstandard2.0.
// The C# compiler emits references to this type; we provide it ourselves.
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
