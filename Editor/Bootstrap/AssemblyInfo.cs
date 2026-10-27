using System.Runtime.CompilerServices;

// Bootstrap compiles independently; only Studio's authoring assembly consumes its implementation.
[assembly: InternalsVisibleTo("KitchenLib.Studio.Editor")]
[assembly: InternalsVisibleTo("KitchenLib.Studio.EditorTests")]
