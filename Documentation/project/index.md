# Manual
This project is aimed to expand and improve existing Unity API as well create extra functionality that can dramatically improve developers quality of life.

## Packages
UnityExt will be divided in a few packages, but all depending on **UnityExt.Core** which contains many auxiliary tools that can be reused for new packages and are mandatory for every project.

### Core
Contains important base classes with features widely used in any kind of projects.
Target improvements of this package includes the following:
 - Facilitate the creation of single-call or looping callbacks without Monobehaviours by means of Activity class or interfaces.
 - In the Activity scope it facilitates the creation and management of thread based loops.
 - Create Timers and keep track of timing with or without laps supporting callbacks to delay call methods.
 - Create Tweens to interpolate properties over time and animate elements by code.
 - Navigate the scene hierarchy sync or async, easily accessing the GameObject tree and its components.
 - Perform web requests with proper loading/completion/parsing routines with minimum overhead
 - OS based tools to call cmdline operations in Win/Unix platforms
