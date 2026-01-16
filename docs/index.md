---
layout: default
---
# ❓ What is Relay for Revit?

![image](https://github.com/user-attachments/assets/6782c5e9-8cf2-4cc1-8daf-a120c97688c2)

Relay is a plugin for Revit developed by [john pierson](johntpierson.com). Relay allows you to add Dynamo graphs (.dyn files) to your ribbon as push button tools.
This is achieved by launching Dynamo in the background (UI-Less) and running the given Dynamo graph. This requires you to have only one version of Dynamo installed and for your Dynamo Graph to be structured in a way that allows you not to require inputs or use custom nodes to feed inputs. More info here: [Working With Inputs](#-working-with-inputs)

# ⚖ License
https://tldrlegal.com/license/gnu-general-public-license-v3-(gpl-3)
Relay is available through the GPL-30 License. This means you can do anything with this code except distribute closed-source versions of the base code in the repo.

| 🙌 **Permissions**        | 📖 **Conditions**          | 💡 **Limitations** |
|:-------------|:------------------|:------|
| 🟢 Commercial use           | 🔵 Disclose source | 🔴 Liability  |
| 🟢 Distribution | 🔵 License and copyright notice   | 🔴 Warranty  |
| 🟢 Modification           | 🔵 Same license      |    |
| 🟢 Patent use           | 🔵 State changes |   |
| 🟢 Private use |  |  |

>💡 Basically, please be nice and contribute / improve this tool if you can.

# 🆘 Support
## 💻 General Support:
This code is provided "As-Is" and without warranty. The idea was to expose how to do this within Revit, while developing a tool for our own internal uses. If you encounter anything that you want to report, please do that on the Github Issues page, 

> 💡 note: there are no guarantees regarding development based on the posted issues

# 🛠 Tested Revit & Dynamo Versions:
- Revit 2020 | Dynamo 2.3.0 *(Tested by the community)*
- Revit 2021 | Dynamo 2.6.1   | Supported by Developer
- Revit 2022 | Dynamo 2.12.0 | Supported by Developer
- Revit 2023 | Dynamo 2.13.0 | Supported by Developer
- Revit 2024 | Dynamo 2.19.0 | Supported by Developer
- Revit 2025 | Dynamo 3.0.0   | Supported by Developer
- Revit 2026 | Dynamo 3.2.x   | Supported by Developer

# ⏯ Usage
## 🔧 Installation:

Preferred Installation Path: ```C:\Users\%username%\AppData\Roaming\Autodesk\Revit\Addins\202x```

| -        | -          |
|:-------------|:------------------|
| For a simple install, simply unzip the folder from releases, to the above directory. Your folder structure should resemble the GIF to the right if you did it correctly.           | ![relayInstall](https://github.com/user-attachments/assets/29fc3e9a-f910-4dff-8e98-089fc9a7f9bc) |



[These instructions](https://github.com/radumg/DynaWeb#alternative-installation-methods) from Radu, describe the process for unzipping folders for Revit add-ins.

| -        | -          |
|:-------------|:------------------|
| ![image](https://github.com/user-attachments/assets/66ce17d9-a548-40ff-91b6-dd9742b52e30) | Relay.addin → This loads the tool into your version of Revit and references the .dll in the Relay folder. |
| - | Relay.dll → The code that actually loads and runs the tool.   |
| ![image](https://github.com/user-attachments/assets/b1756ad8-1f29-4ce5-9958-9a652daaae99) | Relay Folder → Contains the .dll file and the Dynamo graph folder structure.      |

> 💡 The above would be the minimum to get started. Below we break down the graph folder structure in more detail.

# 🏗 Dynamo Graph Structure:
_How to structure your folder for success._

- Relay Graphs - The main directory that the plugin looks at.
  - SubFolder 1 - Creates a panel in Revit with this folder name.
    - Folder1DynamoGraph1.dyn - Unique Dynamo graph name.
    -  Folder1DynamoGraph1_16.png - (optional) 16x16 px image for small images.
    - Folder1DynamoGraph1_32.png - (optional) 32x32 px image for small images.
  - SubFolder 2 - Creates a panel in Revit with this folder name.
    - Folder2DynamoGraph1.dyn - *Unique Dynamo graph name.*
    - Folder2DynamoGraph1_16.png - *(optional)* *16x16 px image for small images.*
    - Folder2DynamoGraph1_32.png - *(optional)* 32*x32 px image for small images.*
    - Folder2DynamoGraph2.dyn - *Unique Dynamo graph name.*
    - Folder2DynamoGraph2_16.png - *(optional)* *16x16 px image for small images.*
    - Folder2DynamoGraph2_32.png - *(optional)* 32*x32 px image for small images.*

# 💡 Example folder structure: (included in base installer):
## 📁 Base Folder Structure:
![image](https://github.com/user-attachments/assets/3bd41fb3-43bf-4538-a53d-f44b946b10f1)

## 📂 Grouped Folder Structure (buttons in panel):

| Single Button Folder View:        | Resulting Panel View (in Revit):          |
|:-------------|:------------------|
| ![image](https://github.com/user-attachments/assets/ce1a9188-71c1-4248-9550-2c8a75496ba2) | ![image](https://github.com/user-attachments/assets/d9b24626-36bd-419d-a599-8a3d2a216289) |
| ![image](https://github.com/user-attachments/assets/d32772e7-a7c0-44a9-9d1c-36afd77a538e) | ![image](https://github.com/user-attachments/assets/696afbeb-51e5-41fc-82c3-70c2f6ec533b) |


# 🏆 Best Practices for Graphs
## 🔌 Working with Inputs:
As you may imagine, what good is a Dynamo graph without being able to interact with it? Dynamo player manages this in its own way, but Relay relies on this interaction being designed by the original Graph author. If you do not build inputs into your graphs with [Data-Shapes](https://data-shapes.io/) or something similar, then your graph will use whatever was selected last.

### Selecting Model Elements:
Using Data-Shapes for selection.
![image](https://github.com/user-attachments/assets/5a49c8f0-5974-4dd2-8e82-ba3f15754320)

# 📦 Managing Packages:
By design, Relay requires your users to have all Packages installed that you use within your Dynamo graphs. This tool does not manage package dependencies for you in any way. If a user runs one of your Dynamo graphs that uses a package and they do not have it, **it simply will not run to completion**.

If managing packages on the fly with Dynamo graphs on the ribbon is a requirement for you. You should definitely check out, [Orkestra from Data-Shapes.io](https://www.orkestra.online/).

### For further reading on making your graph UI-friendly, here are some great resources:

[Winning-Revit-Dynamo-Player-2017](https://www.autodesk.com/autodesk-university/class/Winning-Revit-Dynamo-Player-2017)

[Dynamo-BIM-Managers-Managing-Dynamo-and-Dynamo](https://www.autodesk.com/autodesk-university/class/Dynamo-BIM-Managers-Managing-Dynamo-and-Dynamo-Managing-Part-1-4-2019)

# 💽 Included Sample DYNs
- About
    - HelloThere.dyn
        - This is the "Hello World" of the tool. Shows how to use a Rhythm node to show a UI dialog from Dynamo.
- Create
    - Place All WallTypes.dyn
        - Demonstrates how to place all wall types from the current Revit model. This is a UI-Less interaction and also demonstrates effective ways to include compatibility for other versions.
- Modify
    - AddPrefixOrSuffixToViews.dyn
        - This graph demonstrates how to build a UI with [Data-Shapes](https://data-shapes.io/) for use within Relay.
    - Override Color.dyn
        - This shows how to change colors of selected elements using a Rhythm node for selection.
- Quantify
    - ChartWallTypeUsage.dyn
        - This graph demonstrates how to build a UI with [Data-Shapes](https://data-shapes.io/) for use within Relay. includes charting and collectors.
