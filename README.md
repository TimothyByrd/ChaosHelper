# ChaosHelper

This tool speeds up the completion of chaos recipes in Path of Exile.

It was inspired by a similar tool promoted by Path Of Matth,
except this tool does not manipulate the PoE game client in any way.

It has two main functions:
- It can automatically write an updated loot filter file as you accumulate chaos recipe items. You still need to refresh your filter in PoE, but it tells you when to.
- It can highlight sets of items in your stash to make selling for chaos more efficient.

#### Table of contents
[TLDR to use it](#h01)<br>
[How to use it](#h02)<br>
[Automatic stuff](#h03)<br>
[Commands and hotkeys](#h04)<br>
[About the template and filter](#h05)<br>
[Security](#h06)<br>
[Configuration details](#h07)<br>
[Troubleshooting](#h08)<br>
[Building the tool](#h09)<br>
[Links](#h10)<br>
[Donation](#h11)<br>

## TLDR to use it
<a name="h01" />

- Configure your account name, poesessid and source filter in settings.jsonc.
- Run both PoE (Windowed) and the tool
- The very first time, type an 'f' in the tool window - you should hear a response.
- Play, reload the loot filter when it tells you.
- Use the highlight items hotkey to highlight sets of items to sell for chaos.

## How to use it
<a name="h02" />

- Put the folder of binaries somewhere.
- Do configuration in settings.jsonc:
    - You must set your account name, poesessid and the name of the source filter.
    - The source filter can be anything you want. By default is should work well using a NeverSink filter.
    - Other configuration is to taste, see "Configuration details" below.
- Start Path of Exile in windowed full-screen or windowed mode.
- Start the ChaosHelper tool
- In Path of Exile:
    - Change zones once for the tool to know where you are.
    - If this is your first time or if you have switched leagues, force an initial generation of the loot filter by typing an 'f' in the tool window.
    - Go to `Options->UI` and select "Chaos Helper" as your loot filter.
- As you play, reload your filter via `Options->UI` when it tells you to.
- When you are ready to sell a batch of recipes:
    - Use the highlight items hotkey to highlight the next batch of things to sell.
    - Use the highlight colors to guide you in Ctrl-clicking items from your stash to your inventory, so the bigger items move first.
    - Use the highlight items hotkey again to highlight the next set.
    - When nothing highlights, you are done for now.
- If you tend to dump everything you find into your quad tab, use the show junk items hotkey to highlight non-recipe items.

## Automatic stuff
<a name="h03" />

The Chaos Helper tool can do the following automatically:

- Given your account and poesessid:
    - Determine your current character and league.
    - Figure out which stash tab to use look at.
- Notice you have changed zones by tailing PoE's client.txt file.
- On changing zones:
    - Grab the stash tab contents
    - See if the filter should be updated.
    - If so, update the filter and play a sound to let you know to reload it.
    - Also, it a highlight is being displayed, changing zones will cancel it.
- Notice you if have switched characters, and recheck the league and stash tab.
- Detect a restart of the PoE process.

What the Chaos Helper tool does not / cannot do is manipulate the PoE game client.
It sends neither keystrokes nor mouse clicks to the game client.
The defined hotkeys are to send input *to the tool*, not to PoE.

As a contrast, the poe_qol tool, when highlighting a set to sell, creates a window above the PoE client for each highlighted item.
When the user clicks on "an item", they are really clicking on that window, which then hides itself and *sends a mouse click in the same screen position to PoE*.
This method makes it easy for anyone with access to the Python source code for poe_qol (and it's been put up on github) to convert it
to "fully automatic" - having the first click in a highlight window do a loop to cause clicks for all the highlighted items to go to PoE.

## Commands and hotkeys
<a name="h04" />

There are five commands you can send to the tool:

1. Highlight items (H): (town only) To highlight sets of items to sell (it tries for two sets).
2. Show junk items (J): (town only) To toggle showing items in your stash tab that are not for the recipe, so you can clear them out.
3. Force update (F): To force a re-read of the stash tab and a write of the filter file.
4. Character check (C): Recheck your character and league. (This should happen automatically when you switch chararacters.)
5. Test pattern (T): (town only) Toggle displaying a test pattern to verify the stash window size is correct. Once good, you can disable any hotkey for it, unless you change monitors.

The commands can be invoked:

1. By typing the appropriate letter (H,J,F,C,T) in the ChaosHelper console window.
- Best for commands you will hardly ever use, like character check and test pattern.
2. By using global hotkeys created by ChaosHelper
- By default they are enabled and use the keys `Alt-H`, `Alt-J`, `Alt-F`, `Alt-C` and `Alt-T`, respectively.
- These hotkeys can be rebound or disabled in settings.jsonc.
- The character check and test pattern command hotkeys are disabled by default, they can be uncommented in settings.jsonc.
- When definining a hotkey, for modifiers, use '^' for Ctrl, '+' for Shift and '!' for Alt.
- For a list of key names/numbers, see https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys
- Remember, these set **global** hotkeys, so be careful not to bork your other programs.
3. If you use the PoE-TradeMacro AutoHotkey script, there are scripts in the `AhkMacros` folder you can copy into your `Documents\PoE-TradeMacro\CustomMacros` folder.
- Since PoE-TradeMacro binds some keys, the scripts come set up to be invoked using `Crtl-H`, `Ctrl-J`, `Ctrl-U`, `Ctrl-S` and `Ctrl-P`, respectively.
- In this case, comment out the hotkeys in setting.jsonc.

## About the template and filter
<a name="h05" />

When it wants to update the loot filter, the tool reads the template file,
looking for a line with a specific pattern. It writes the new filter file,
copying all the lines from the template, and inserting the chaos filter code
in the specified place. The template and the filter must be different files.

The template needs to be specified in the `template` entry.
I suggest using an existing filter - one that doesn't already show all the chaos recipe items, otherwise there is no point.
Since the tool will write to a different file, your existing filter will stay safe.

The filter it generates is specified by the `filter` entry and defaults to `Chaos Helper.filter`. 

You can set the place in the filter where the chaos recipe code will go by configuring the `filterMarker` entry.
For example, if you have your own custom loot filter and want to use it, you could set `filterMarker` to "%%" and then add a line to your filter in the appropriate place reading "#%%".
Your filter will still work as before, and the tool will be able to use it as a template.

By default, the tool looks for a line containing the text "section displays 20% quality rares" in the template file.
For Neversink filters, this seems to put the chaos recipe section in a good place.

**NOTE:** Make sure the text specified in `filterMarker` only occurs once in the filter template, at the place you want the chaos recipe code to go.
For example, when using a Neversink filter it would be tempting to use something like "[[3100]] OVERRIDE AREA 2" as the `filterMarker'.
However that text occurs *twice* in the filter - once in the place we want the code and once in the table of contents at the top of the file.
So using that text would cause the chaos recipe code to be inserted in the filter in both places.
This would probably not be what you want, for example, it would override the normal hightlight for rare 6-socket armor.

## Security
<a name="h06" />

I trust myself, but you shouldn't. I suggest building the tool yourself (see below).
Or for security, limit what the tool can do via your firewall.
The only HTTP calls the tool should make are all to www.pathofexile.com.
They are all HTTP GETs, so they aren't changing anything on the server.
Except perversely there is an HTTP POST to www.pathofexile.com/character-window/get-items,
because the GGG web developers are not consistent and used one POST with form data instead of a GET with query parameters.

## Configuration details
<a name="h07" />

There are three things you must configure in settings.jsonc to use the tool:
1. `account`: your Path of Exile account name.
2. `poesessid`: the session Id for your currrent login to www.pathofexile.com.
3. `template`: the template file used to create/update the filter
- An existing Neversink filter should work with the defaults.
- See "About the template and filter" for more info.

`townZones` defines the areas that are considered to be "towns".
The commands noted as "town only" - highlight items, show junk items and test pattern - will only work when the tool thinks you are in a town zone.
If no town zones are defined, then the tool will treat every area as a town zone.
If you want protection from accidentally pressing one of the hightlight hotkeys while in combat, then uncomment the list of zones, and add you hideout to the list.
    
I suggest leaving `league`, `character`, `tabName`, `tabIndex` and `isQuadTab` at the defaults.
This will cause to the tool to auto-determine the values, which is good when there are multiple leagues available.
In particular, `tabIndex` is difficult, because the same tab can change from league to league and can depend on if there are Remove-only tabs visible.

`filter` ("Chaos Helper") is the name of the .filter file to create/update - just don't make it the same as `template`.
See "About the template and filter" for more info.

`filterMarker` sets the text in the template file that will mark where to put the chaos recipe section.
See "About the template and filter" for more info.

`filterColor` ("80 0 220") sets the border and text color for highlighted items in the updated filter.
It can be specified as a hex number like "0x4600e6" or a filter file color like "70 0 230".

`maxSets` (12) is the number of chaos recipe sets the tool will aim to collect in your stash tab.
It uses this number to determine when to stop showing item classes in the loot filter.
A quad tab can hold about 16 complete sets (and a regular tab about 4).
Since you want to be able to ctrl-click to quickly dump items into the tab (and the tab can get messy over time - this is "fragmentation") setting this to 10 or 12 seems reasonable.

`maxIlvl` (-1) is the maximum ilvl of items to highlight in the loot filter.
It can be set to 74 to not show regal recipe items.
The default of -1 means to give the recipe highlight to all applicable items of ilvl 60 and above.
Note that setting `maxIlvl` will not prevent the filter from showing any rares that it would have shown before.
Only the highlighting may change from the chaos recipe highlight to what was already in the filter.
 
`allowIDedSets` (false) sets whether the tool will support making sets including IDed items.
- Defaults to false because it causes extra vendor trips.
- I do this because I ID two-stone rings while levelling.
- Also has the filter show IDed rare rings, like breech rings.
- In an IDed set, both rings must be IDed, and other slots will prefer IDed items.
- The tool will not mix an IDed set and an un-IDed set in one sale.

`singleSetsForChaos` (false) sets whether the tool will highlight a single set at a time
when starting to get ilvl 75+ items to maximize chaos over regals. For example, if the stash
contains one ilvl 74 glove and one ilvl 74 helmet and everything else is ilvl 75+,
it will hihglight them in two different sales.
It defaults to false, because the extra vendor trips waste time.

`ignoreMaxSets` causes the specified item classes to ignore the `maxSets` setting.
For example, setting `ignoreMaxSets` to "Rings,Amulets,Belts" when `maxSets` is 12,
will cause the filter to keep highlighting rings, amulets and belts even when there are 12 or more of them in the stash tab.
Possible item classes are BodyArmours, Helmets, Gloves, Boots, OneHandWeapons, Belts, Amulets, and Rings

`ignoreMaxIlvl` causes the specified item classes to ignore the `maxIlvl` setting, if it is set.
For example, setting `ignoreMaxIlvl` to "Rings,Amulets,Belts" when `maxIlvl` is 74
will cause the filter to keep highlighting rings, amulets and belts at higher ilvls.
Possible item classes are BodyArmours, Helmets, Gloves, Boots, OneHandWeapons, Belts, Amulets, and Rings

`highlightItemsHotkey`, `showJunkItemsHotkey`, `forceUpdateHotkey`, `characterCheckHotkey` and `testModeHotkey` can be set to enable global hotkeys to execute ChaosHelper commands.
If not defined, the hotkeys are not enabled.
See "Commands and hotkeys" for more info.
	
`includeInventoryOnForce` (false) will cause items in the character's inventory to be included when a force update command is executed.
It ought to be useful for when a Keepers of the Trove pack gives you all the gloves you need,
but it depends on your inventory on the website having been updated, and there's a bit of a lag for that.

`clientTxt` defines where to find the PoE client.txt log file, which the tool uses to track zone changes.
The tool tries to auto-determine this, but if Path of Exile was installed to a custom folder,
or if both the stand alone client and the Steam client are installed, this value may need to be set.
(The tool will look for the client.txt for the stand alone client, first.)
The Epic client is not yet supported, and `clientTxt` must be configured for it.

`processName` defines the process name for the running game.
The tool tries to auto-determine this, looking for "PathOfExile_x64" for the stand alone client and "PathOfExile_x64Steam" for the Steam client.
The Epic client is not yet supported, and `processName` must be configured for it.

The tool simply finds the first matching process.
To run two copies of Path of Exile while using the tool,
run one PoE using the stand alone client and one PoE using the Steam client,
and configure `processName` (and `clientTxt`) for the copy you want the tool to go with.

`stashPageXYWH` ([ 0, 0, 0, 0 ]) specifies the rectangle in the PoE client window where the stash tab grid is.
It usually auto-determines correctly, but may need to be specified for certain monitors.

If you need to set a custom value, take a screenshot of your stash tab,then open the screen shot in a program like IrfanView.
Make a select rectangle over the grid part of the tab, and determine the X,Y,Height,Width of the selection.
You can test the values by typing 't' in the ChaosHelper console to execute the test pattern command.

`highlightColors` ([ "0xffffff", "0xffff00", "0x00ff00", "0x0000ff" ]) is an array of color values (as strings)
to specify stash highlight colours for armour/weapons, helmets/gloves/boots, belts, and rings/amulets, respectively.
There must be four colors in the array and they must be hex number strings of the forms "0xRRGGBB".

## Troubleshooting
<a name="h08" />

1. You must configure your account name, poesessid and source filter in settings.jsonc.
3. If you are just starting out, type an 'f' in the ChaosHelper window to force an initial generation of the loot filter, and then change zones in-game.
4. Don't do a text selection in the ChaosHelper console window. If you do, the tool will be stopped until the selection is removed.
5. Check for error messages in the tool window. If the tool window has closed, there should be log files in the `logs` folder.
6. If you are playing in an non-English PoE, will need to build the tool yourself and translate some strings - particularly the "You have entered" it looks for in client.txt. (But tell me what it took, and I may make this easier in the future.)

## Building the tool
<a name="h09" />

Building the tool is pretty easy once you clone the git repo:
- It builds in Visual Studio 2019 Community Edition
- The code is all C#.
- You should be able to open ChaosHelper.sln and then build/run the tool
    - Still need to configure setting.jsonc, though.

## Links
<a name="h10" />

- Originally inspired by [poe_qolV2](https://github.com/notablackbear/poe_qolV2), which uses Python.
- The overlay code comes from [Overlay.NET](https://github.com/lolp1/Overlay.NET). I copied in this code because there are posts in the project that the published Nuget package is not up to date with source code.
- The hotkey code is originally from [StackOverflow](https://stackoverflow.com/questions/3654787/global-hotkey-in-console-application/3654821).

## Donation
<a name="h11" />

If this project helped you, you can help me :) 

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=XE5JR3FR458ZE&currency_code=USD)
