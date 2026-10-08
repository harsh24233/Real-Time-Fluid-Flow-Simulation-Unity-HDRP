# Real-Time Fluid Flow Simulation — Unity HDRP

A Unity-based real-time fluid flow simulation framework for an enclosed pipeline system, developed using the High Definition Render Pipeline (HDRP). The project combines C# pipeline-control components with GPU compute-shader functionality for fluid simulation and processing.

---

## 1. Project Overview

This project focuses on developing a real-time fluid flow simulation within a 3D pipeline environment.

The system is structured around a pipeline model and provides software components for:

- Fluid flow simulation
- Pipeline flow control
- Pump control
- Valve control
- Pipeline system management
- Camera control
- Simulation user interface
- GPU compute operations

The project is intended as a research and development platform for continued work on real-time fluid simulation and visualization.

---

## 2. Technical Architecture

The project follows a component-based Unity architecture.

```text
                         ┌──────────────────────────┐
                         │      Unity HDRP Scene    │
                         │   OutdoorsScene.unity    │
                         └────────────┬─────────────┘
                                      │
                                      ▼
                         ┌──────────────────────────┐
                         │     Pipeline System      │
                         │   PipelineSystem.cs      │
                         └────────────┬─────────────┘
                                      │
             ┌────────────────────────┼────────────────────────┐
             │                        │                        │
             ▼                        ▼                        ▼
   ┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
   │ Fluid Simulation │    │ Flow / Equipment │    │ User Interaction │
   │                  │    │                  │    │                  │
   │ FluidFlow        │    │ PipeFlow         │    │ Pipeline UI      │
   │ Simulation.cs    │    │ Controller.cs    │    │ Camera Control   │
   └────────┬─────────┘    │ PumpController   │    └──────────────────┘
            │              │ ValveController  │
            │              └──────────────────┘
            ▼
   ┌──────────────────────────────┐
   │     GPU Compute Processing   │
   │      SPHSortCompute.compute  │
   └──────────────────────────────┘
```

---

## 3. Technology Stack

| Component | Technology |
|---|---|
| Game / Simulation Engine | Unity 6 |
| Render Pipeline | High Definition Render Pipeline (HDRP) |
| Programming Language | C# |
| GPU Processing | Compute Shaders |
| Fluid Simulation Approach | Smoothed Particle Hydrodynamics (SPH) |
| 3D Pipeline Asset | FBX |
| Input System | Unity Input System |

---

## 4. Verified Project Versions

| Component | Version |
|---|---|
| Unity Editor | `6000.6.1f1` |
| HDRP | `17.7.0` |
| Input System | `1.20.0` |

The project should preferably be opened using the specified Unity Editor version to avoid compatibility or package-version issues.

---

## 5. Project Structure

```text
HDRP/
│
├── Assets/
│   │
│   ├── OutdoorsScene.unity
│   ├── pipeline.fbx
│   │
│   ├── Resources/
│   │   └── SPHSortCompute.compute
│   │
│   ├── Scripts/
│   │   └── Pipeline/
│   │       ├── FluidFlowSimulation.cs
│   │       ├── PipeFlowController.cs
│   │       ├── PipelineCameraController.cs
│   │       ├── PipelineSystem.cs
│   │       ├── PipelineUIManager.cs
│   │       ├── PumpController.cs
│   │       ├── ValveController.cs
│   │       │
│   │       └── Editor/
│   │           ├── FluidFlowSimulationEditor.cs
│   │           └── PipelineSetupEditor.cs
│   │
│   └── Settings/
│       ├── HDRP Balanced.asset
│       ├── HDRP High Fidelity.asset
│       ├── HDRP Performant.asset
│       ├── SkyandFogSettingsProfile.asset
│       │
│       └── HDRPDefaultResources/
│           ├── DefaultLookDevProfile.asset
│           ├── DefaultSettingsVolumeProfile.asset
│           ├── HDRenderPipelineAsset.asset
│           └── HDRenderPipelineGlobalSettings.asset
│
├── Packages/
├── ProjectSettings/
├── .gitignore
├── .vsconfig
└── README.md
```

---

## 6. Core Components

### 6.1 Fluid Flow Simulation

**File:** `Assets/Scripts/Pipeline/FluidFlowSimulation.cs`

Provides the main fluid-flow simulation component used by the pipeline system.

The corresponding custom Unity editor is located at:

```text
Assets/Scripts/Pipeline/Editor/FluidFlowSimulationEditor.cs
```

---

### 6.2 Pipeline System

**File:** `PipelineSystem.cs`

Acts as a central component for coordinating the pipeline system and its associated simulation/control components.

---

### 6.3 Pipe Flow Controller

**File:** `PipeFlowController.cs`

Provides flow-related control for the pipeline system.

---

### 6.4 Pump Controller

**File:** `PumpController.cs`

Provides control functionality for the pump within the pipeline environment.

---

### 6.5 Valve Controller

**File:** `ValveController.cs`

Provides valve-related control for the pipeline flow system.

---

### 6.6 Pipeline Camera Controller

**File:** `PipelineCameraController.cs`

Controls the camera used for viewing the pipeline simulation environment.

---

### 6.7 Pipeline UI Manager

**File:** `PipelineUIManager.cs`

Handles the user-interface functionality associated with the pipeline simulation.

---

### 6.8 GPU Compute Shader

**File:**

```text
Assets/Resources/SPHSortCompute.compute
```

Provides GPU compute functionality associated with the SPH-based simulation processing.

---

## 7. Pipeline Model

The primary pipeline model is:

```text
Assets/pipeline.fbx
```

The model is integrated into the Unity project and forms the geometric environment in which the pipeline simulation operates.

---

## 8. HDRP Configuration

The project uses Unity's High Definition Render Pipeline.

HDRP-related configuration and rendering resources are maintained under:

```text
Assets/Settings/
```

The project currently contains multiple HDRP configuration assets:

- `HDRP Performant.asset`
- `HDRP Balanced.asset`
- `HDRP High Fidelity.asset`

HDRP default resources and global rendering configuration are also included under:

```text
Assets/Settings/HDRPDefaultResources/
```

---

## 9. Main Scene

The primary Unity scene is:

```text
Assets/OutdoorsScene.unity
```

After opening the project in Unity, this scene should be used as the starting point for inspecting and continuing development of the simulation.

---

## 10. Getting Started

### Requirements

The project should be opened with:

```text
Unity 6000.6.1f1
```

with:

```text
HDRP 17.7.0
```

### Setup

1. Clone the repository.
2. Open Unity Hub.
3. Select **Add Project from Disk**.
4. Select the cloned project directory.
5. Open it using Unity `6000.6.1f1`.
6. Allow Unity to import and regenerate its local project cache.
7. Open:

```text
Assets/OutdoorsScene.unity
```

Unity will regenerate folders such as `Library/` locally when required.

---

## 11. Packages

The project package configuration is stored in:

```text
Packages/manifest.json
Packages/packages-lock.json
```

The repository retains these files so that Unity can restore the required package dependencies when the project is opened.

The primary rendering package is:

```text
com.unity.render-pipelines.high-definition
```

Version:

```text
17.7.0
```

---

## 12. Version Control

The repository contains the source project required for continued development.

Unity-generated directories are intentionally excluded from version control:

```text
Library/
Temp/
Obj/
Build/
Builds/
Logs/
UserSettings/
```

Generated IDE/project files are also excluded.

Unity `.meta` files are retained because they contain asset GUID information required to preserve references between project assets.

---

## 13. Development Status

### Implemented Project Components

- Unity HDRP project configuration
- Pipeline model integration
- Pipeline system architecture
- Fluid flow simulation component
- SPH-related GPU compute shader
- Pipe flow control
- Pump control
- Valve control
- Pipeline camera control
- Pipeline UI management
- Custom Unity editor tools
- Multiple HDRP rendering configurations

---

## 14. Future Development

The project is intended for continued development and experimentation.

Potential development areas include:

- Further development and refinement of the SPH fluid simulation
- Improving fluid behaviour inside the pipeline
- Improving pump and valve interaction with the simulation
- Enhancing real-time fluid visualization
- GPU performance optimization
- Improving simulation stability
- Increasing the physical accuracy of the simulation
- Testing and validating simulation behaviour under different pipeline configurations

---

## 15. Research Handoff

This repository is prepared as a continuation point for further research and development of the pipeline fluid-flow simulation.

The repository contains the Unity project source, project configuration, required package configuration, HDRP settings, simulation scripts, pipeline model, and compute-shader resources required for continued work.

### Project Author

**Harsh**  
IIIT Delhi

### Continued Development

**Jayant Raghav**

---

## 16. Notes for Contributors

- Use the specified Unity version whenever possible.
- Do not commit the Unity `Library/`, `Temp/`, `Logs/`, or other generated directories.
- Do not delete Unity `.meta` files.
- Preserve the existing package configuration unless a package change is required.
- Test changes in the main scene before committing major modifications.
- Document significant changes to the simulation architecture or GPU computation.

---

## 17. Academic / Research Use

This project is developed for academic and research purposes at IIIT Delhi.

Further development and experimentation should maintain clear documentation of changes to the simulation, rendering configuration, and computational methods.
