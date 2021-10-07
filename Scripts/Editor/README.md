# Editor tools

## Animator Extensions
Some [Harmony](https://github.com/pardeike/Harmony) -based patches to Unitys Animator window:  
* Layer copy-pasting and duplication (including cross-controller) via context menu or keyboard shortcuts
* F2 keyboard shortcut to rename selected layer
* Instead of the annoying list scrollbar reset, get new or edited layer in view

![Animator Extensions context menu](.img/AnimatorExtensions_Context.png)

## Material Cleaner
Full window to display material keywords, property stats and and allow quick cleanup of unused ones.  
Also has single click way to cleanup multiple materials via Toolbar->`Tools/DJL/Cleanup Material properties`.  
Skips materials with missing/invalid shaders so hopefully should not damage assets.

![Material cleaner window](.img/MaterialCleaner.png)

## Point-cloud mesh creator
![Point Mesh Creator window](.img/PointMeshCreator.jpg)

## Material object locator
With some material shown in inspector, right-click its header to select and highlight material object in project window, just like Unity allows to select the shader

## Mesh asset saver
Right click `Mesh Filter` or `Skinned Mesh Renderer` component to save its mesh as an asset. Useful for saving runtime(script)-generated meshes.  
One of the options allows to **overwrite the mesh bounds** with the ones specified in `Skinned Mesh Renderer` component.

## Mesh bounds inspector
Makes `Mesh Filter` components show bounding box like `Skinned Mesh Renderer`s do