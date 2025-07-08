# IRIS-Viz

**A powerful Unity visualization framework for bridging simulation and reality**

## Overview

IRIS-Viz (successor to IRXR) is a comprehensive Unity package developed by the Intuitive Robots Lab that enables seamless visualization of objects in both simulated environments and real-world contexts. By providing a robust framework for handling complex 3D data, IRIS-Viz empowers developers to create immersive and accurate representations for robotics, industrial automation, digital twins, and more.

## Key Features

- **High-Performance Visualization** - Render complex meshes and dense point clouds with optimized performance
- **Dual-Environment Support** - Seamlessly bridge simulation and real-world data in a unified visual framework
- **Large Data Handling** - Support for transmitting and rendering mesh data up to 10MB
- **ZeroMQ Communication** - Efficient, reliable data transfer using industry-standard messaging protocols
- **Modular Architecture** - Separate components for scene loading and client operations, enabling flexible implementation
- **Cross-Platform Compatibility** - Works across multiple Unity deployment targets

## System Requirements

- Unity 6000.0 or higher
- NuGetForUnity (for dependency management)
- NetMQ package (ZeroMQ implementation for .NET)

## Installation

### Step 1: Install Prerequisites

#### NuGetForUnity
1. In Unity, navigate to **Window > Package Manager**
2. Click the **+** button and select **Add package from git URL...**
3. Enter: `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`
4. Click **Add**

#### NetMQ
1. After installing NuGetForUnity and relaunch the Unity Editor, go to **NuGet > Manage NuGet Packages**
2. Search for **"NetMQ"**
3. Select and install the appropriate version

### Step 2: Install IRIS-Viz

#### Option 1: Using Git URL (Recommended)
Add the following to your `manifest.json` in your Unity project's `Packages` folder:

```json
{
  "dependencies": {
    "com.intuitiverobotslab.iris-viz": "https://github.com/intuitiverobotslab/IRIS-Viz.git"
  }
}
```

#### Option 2: Unity Package Manager
1. Open the Package Manager in Unity (**Window > Package Manager**)
2. Click the **+** button
3. Select **Add package from git URL**
4. Enter: `https://github.com/intuitiverobotslab/IRIS-Viz.git`


## Architecture

IRIS-Viz consists of two primary components:

- **IRISNode**: Core communication layer that handles data transfer between sources and Unity
- **SceneLoader**: Visualization toolkit for rendering 3D data within Unity scenes

```
IRIS-Viz
├── IRISNode
│   ├── Communication
│   ├── DataParsing
│   └── Events
└── SceneLoader
    ├── MeshHandling
    ├── PointCloudRendering
    └── SceneManagement
```

## Documentation

For complete API references, tutorials, and advanced usage scenarios, please refer to our [comprehensive documentation](Documentation~/index.md).

## Examples

The package includes several example scenes demonstrating different visualization capabilities:

- Basic mesh visualization
- Real-time point cloud streaming
- Multi-object scene composition
- Simulation-to-reality comparison

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## About Intuitive Robots Lab

The [Intuitive Robots Lab](https://www.irl.iar.kit.edu/) at Karlsruhe Institute of Technology researches next-generation robotics systems with natural human-robot interaction and intuitive control mechanisms.

## Contact

- **Developer**: Intuitive Robots Lab
- **Email**: xinkai.jiang@kit.edu
- **Website**: https://www.irl.iar.kit.edu/team.php
- **GitHub**: [intuitiverobotslab](https://github.com/intuitiverobotslab)