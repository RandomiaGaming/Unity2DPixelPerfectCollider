# Unity 2D Pixel Perfect Collider

Unity 2d pixel perfect collider is a simple c# script that comes jam packed with features to help you automatically generate pixel perfect colliders.

# How To Use

To get started download Unity2DPixelPerfectCollider.cs and save it into your unity project's assets folder. This one file contains all the code you will need. From there you can just add the PixelCollider2D component to a game object and press regenerate collider to automatically create a pixel perfect polygon collider for that game object.

# What About Tilemaps?

Generating a pixel perfect outline for each game object is fine for simple projects but for more complex games with hundreds of game objects it's not realistic nor optimized. Instead select window>pixel physics shape editor to open a handy little window. From there you can drag and drop textures or folders full of textures to add them to the selected textures list. Then when you press generate and apply physics shapes it will automatically generate a pixel perfect physics shape for each of your textures. A physics shape is a feature of the unity sprite editor which allows users to define the default shape for colliders when representing that texture. Additionally physics shapes are used for tile hitboxes, so by setting a pixel perfect physics shape we can ensure that all our tiles have the correct colliders as well.

# Readability

In order to generate a pixel perfect outline of a given or texture the pixel data of the texture must be readable. In unity textures can be readable or not depending on their asset import settings. To ensure that you don't run into issues make sure that you enable read/write for any textures you wish to generate pixel perfect colliders for. You can do this by right clicking on a texture in the project window and selecting properties. Then check the box under advanced>read/write. Alternatively my library has OnTextureUnreadable settings which can be configured to determine what should happen if a texture is unreadable. For more information about these settings read the comments at the very top of Unity2DPixelPerfectCollider.cs.

# Thank You

Huge shoutout and thanks to all the people who have starred, forked, or commented about this repo. Seeing all the amazing games people have made using my code really helped motivate me to keep working on this project and adding new features. Y'all are the best!
