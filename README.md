# MakePortable
Make your .Net code easier to reuse


## TL;DR;
This utility scans a .Net Core project folder, then creates and align a mirror folder where a Portable (PCL) project keeps the LINKS to the source modules.


## Motivation
With the advent of the .Net Core you are tempted to (re)write a library for that framework, so that it become cross-platforms. However, once you create any .Net Core project you cannot easily link it to a ".Net classic" project (e.g. WPF).

The suggested way is to publish the .Net Core component as a NuGet package, then embed it in the non-Core project. Although this might be the most elegant way, there are times that you don't want to walk this way. For instance, you're just playing with some code, and the NuGet publication is just a waste of time and resources.

Since most of the non-UI code is valid for any platform, an easy way could be to have another project containing the LINKS to the source modules. Although the "portable" (PCL) projects are in the fading phase, they're still a good solution for all the wide-usage contexts.


## First setup
Compile the above code, then grab the resulting "EXE" and copy/move in any system-wide location. The aim is the ability to invoke the application from any command-prompt.


## Everyday usage
Locate your source .Net Core solution folder. You can directly use the Windows command-prompt, or use the File Explorer to open the command-prompt at the desired location.

The files/folders structure is typically shaped as follows:
```
MySolution
  global.json
  src
    MyProject
      myproject.xproj
      Properties
      ...
    MyProject.Test
      ...
```
Start the process by typing (as for the above example):
```
MakePortable src\MyProject
```
That is, specify the relative folder belonging the source project.

**DO NOT** add any extra file name (e.g. "myproject.xproj"): at the moment the utility supports just **ONE** project per folder, and assumes it match with the folder.

When no errors break the process, the program terminates instantly with a simple message of success.

The resulting project is contained in a special folder having the **SAME** name of the source project, plus a "_PORTABLE" tail. Inside, you won't find much: just the new "classic" project (*.csproj), and one or more folders as mirror of the original structure. Most of the folders are empty.

Once you link the portable project (or compile as stand-alone), you probably have to manage the external references, such as NuGets, other projects and so away. This sounds annoying, but sometimes it's not straightforward to match the equivalent references.

**NOTE** you should run the utility every time you modify the source project structure.

## Coding details
As for this first release, the target project template is very basic: just plain, old C#. Of course, it's pretty easy to add new templates, improve the conversion, and enrich the utility with some useful options.

Whereas a piece of code is impossible to share across different frameworks, there's a project-wide conditional constant named "PORTABLE".
