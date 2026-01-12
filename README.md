# [DH2413 AGI : Splash Mural]


This is the repo for "Splash Mural", A project for the DH2413 Advanced interaction and graphics course at KTH 2025. 
## 🛠 Technical Specifications

* **Unity Version:** `2022.3.62f1`   
* **Render Pipeline:** Built-in Render Pipeline (BRP)

---

## 🚀 Getting Started

This project contains the unity application. To run it properly an additional repo is required, namely: [AGI-webclient](https://github.com/ShibireX/agi-webclient/tree/main). AGI-webclient connects the phones to the unity application, sends the phone data and contains the UI for the phone controller. 

### 1. Prerequisites

Before opening the project, ensure you have the following installed and configured:

* **AGI WebClient - Phone Motion Controller** .
* **An android or IOS Device** Phones are used as controllers.
* **AGI-drawing** Contains the unity application

### 2. Installation

1.  **Clone the repository:**
    ```bash
    git clone agi-drawing
    ```
2.  **Open in Unity:**
    * Open Unity Hub.
    * Click **Add** -> **Add project from disk**.
    * Select the root folder of this repository.
    * Ensure the version is set to `2022.3.62f1`.

### 3. How to Run 


1.  **Setup the Webcleint** Setup the Weblcient according to its readme. 
2.  **Start the unity application in the editor** Start the project, project has not been tested as a build. Run it in the editor. 
3.  **Connect the phone** On the phone, press connect and the calibrate button to join 
4.  **play** if the connection is working, then values should be seen in the console in the webclient and paint should be splashed in the unity application. 

---

## 🎮 Controls

Phone acts as the controller for each of the 4 players. 1 to 4 players are supported. 
---

## ⚠️ Important Notes

* **Check WIFI/ip configs** Check that both the phone and the computer is connected to the same wifi and has the same IP configs. 
* * **Calibrate phone** Phones might have different orientations, this can be changed in the phone UI bu flipping each of the axises. IF the phone desynces with the virtual brush then point the phone towards the screen and press calibrate. 
* **If no paint is shown** Check connection issues or accelration threshold inside the unity project.  

