# IRIS-Viz (On processing to replace IRXR)

A Unity package for visualizing objects in simulation and real-world contexts, enabling developers to create immersive and accurate representations for a variety of applications.

## Description

IRIS-Viz provides powerful visualization tools for Unity that allow seamless integration between simulated environments and real-world data. Developed by the Intuitive Robots Lab, this package simplifies the process of creating, managing, and displaying complex 3D data including meshes and point clouds.

## Features

- Real-time visualization of simulation and real-world data
- Support for large mesh data transmission (up to 10MB)
- ZeroMQ-based communication for efficient data transfer
- Modular structure with separate components for scene loading and client operations

## Installation

### Option 1: Using Git URL (Recommended)

Add the following to your `manifest.json` in your Unity project's `Packages` folder:

```json
{
  "dependencies": {
    "com.intuitiverobotslab.iris-viz": "https://github.com/intuitiverobotslab/IRIS-Viz.git"
  }
}
```

### Option 2: Unity Package Manager

1. Open the Package Manager in Unity (Window > Package Manager)
2. Click the "+" button
3. Select "Add package from git URL"
4. Enter: `https://github.com/intuitiverobotslab/IRIS-Viz.git`

## Requirements

- Unity 6000.0 or higher
- NuGetForUnity (for handling NetMQ dependency)
- NetMQ package (installed via NuGetForUnity)

## Usage

```csharp
using IRIS;
using IntuitivoRobotsLab.IRIS_Viz;

// Example code will be provided here
```

## Structure

- **IRISClient**: Core client functionality for connecting to data sources
- **SceneLoader**: Tools for loading and visualizing 3D data in Unity scenes

## Documentation

For more detailed information, see the [Documentation](Documentation~/index.md).

## License

[Your License Information Here]

## Contact

- **Developer**: Intuitive Robots Lab
- **Email**: xinkai.jiang@kit.edu
- **Website**: https://www.irl.iar.kit.edu/team.php