using System.Reflection;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#if KK
[assembly: AssemblyTitle("ModBoneImplantor for Koikatsu")]
#elif EC
[assembly: AssemblyTitle("ModBoneImplantor for EmotionCreators")]
#endif
[assembly: AssemblyDescription("Needed by some clothing mods to add dynamic bones")]
[assembly: AssemblyCompany("https://github.com/IllusionMods/ModBoneImplantor")]
[assembly: AssemblyProduct("ModBoneImplantor")]
[assembly: AssemblyCopyright("Copyright ©  2019")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("bc6f1689-cc99-4980-83b9-9bfb4e5f06d5")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion(ModBoneImplantor.ModBoneImplantor.Version)]
