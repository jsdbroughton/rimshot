# Rimshot

<img src="https://github.com/jsdbroughton/rimshot/blob/main/logo.png?raw=true" alt="logo" width="200"/>

## [R]ealtime [I]ssue [M]anagement with [S]peckle - HOT! 🔥

[https://rimshot.app/](https://rimshot.app/issues/vWomnz77p44ZW4Hi350B)

## Premise ##

During the Great Lockdown of the last two years, in what has so quickly become a remote-working, online-first-meetings, broadcast only way of doing business. One really striking aspect of the AECO world, away from the Teams, Google Docs, Zoom and the like, was that the manner of Model Analysis and review has not been in the same class of technological delivery.

I've sat through many dozens of streamed Navisworks sessions and engagement amongst teams has definitely waned or defaulted to a broadcast rather than a collaboration. Attention spent managing the remote experience means the analyst is taxed to the point of only making scant notes about issues, and contributors may not all be heard to register their input.

For this professional/social need I introduce Rimshot - Realtime Issue Management with Speckle.

Utilising two aspects that I admire of Speckle: 

1. Information Hub and low-friction data transfer.
2. Bring people to the Model. Speckle is multiplayer.

[<img src="https://img.youtube.com/vi/1lUDgHJP7H4/maxresdefault.jpg" alt="logo" width="300"/><br />Watch the submission video](https://www.youtube.com/watch?v=1lUDgHJP7H4)

## Usage ##
### Adding details to issues 
1. Install the addin as a application plugin bunmdle
2. Launch Navisworks
3. Select a project and workshop (already created in the rimshot database)
4. Select an issue to commit to
5. Elements selected in Navisworks will be committed as Speckle Mesh to the corresponding branch (created new if not existing)
6. Selecting **Snapshot** adds an image to the existing issue of whatever is visible in the viewport. If no issue is selected, snapshotting ceates a new issue and corresponding branch.

### Contributors
1. The analyst can share a unique secure URL to the workshop record. (https://rimshot.app/issues/{UNIQUE ID})
2. All visitors to that web page can see images and Speckle commits being added. 
3. "Follow-mode" scrolls to the issue currently being presented by the Analyst. Selecting a different issue cancels Follow Mode
4. All contributors can comment and edit the descriptive and the metadata fields.


## Components ##

The hackathon submission consists of:

### An installed webview addin for Navisworks.
<img src="https://user-images.githubusercontent.com/760691/168424755-e6e887e3-89f0-42aa-9f43-ee712722ace1.png" alt="logo" width="300"/>
* Model elements selected in Navis will be committed and synced to the app. 
* Model elements are converted on the fly while the discussion continues.
* The addin allows for immediate viewport screen capture.

### An Issue Mangement workshop capture interface.
<img src="https://user-images.githubusercontent.com/760691/168424792-bcc5870c-e63c-4996-9a8c-4b997917f0b4.png" alt="logo" width="300"/>
* Structuring the database around a collaborative experience aids quality of Issue resolution. 

### A realtime multi-party data capture app and database for desktop and mobile devices.
<img src="https://user-images.githubusercontent.com/760691/168424849-7aca1839-bf19-4226-a1e6-f6d651f1158f.png" alt="logo" width="300"/> <img src="https://user-images.githubusercontent.com/760691/168424963-9d311426-499f-4997-bd9a-5f119f70e821.gif" alt="logo" width="300"/>
* Using a real-time database allows for all authenticate users to comment and properly describe and issue and its path to resolution.* The responsive app design scales to full screen to handheld.
* Contributions can be immediate and collaborative.
* Backend webhook and cloud function support to reinforce the Navis<>App interaction and capture events that may fail due to connecttivity issues or lag. Both events may happen without conflict and eventual consistency is assured. 

### A pattern for managing Model based issues within the Speckle interface.
<img src="https://user-images.githubusercontent.com/760691/168424905-7967c8c1-0065-44dd-8142-eefc0a5a453d.png" alt="logo" width="300"/>
* Each issue is a separate Speckle branch. 
* Multiple sub-issues can be made as separate commits to the branch.
* New issue branches are created automatically

## Roadmap Post Hackathon ##

* Proper linking with Auth flow and project/stream selection.
* Syncing of Comments between the two databases - near realtime.
* More responsive syncing. Allow Speckle to push to the app (perhaps closing out issues, 3rd party commits)
* Sorting out the 🔥HOT MESS🔥 of the Navisworks geometry translation.
<img src="https://user-images.githubusercontent.com/760691/168424674-539b5dd1-db83-4bdf-98aa-b4cfd2bc1d0e.png" alt="logo" width="500"/>

* Integate the Speckle Viewer rather than embed views.
* Responsive Event driven model analysis to push metadata into the issue record.
