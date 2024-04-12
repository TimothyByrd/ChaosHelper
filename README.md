# ChaosHelper

This tool eases the completion of chaos recipes in Path of Exile.

![](./sample1.png?raw=true)

It was inspired by a similar tool promoted by Path Of Matth,

It has two main functions:
- It can automatically write an updated loot filter file as you accumulate chaos recipe items. You still need to refresh your filter in PoE, but it tells you when to.
- It can highlight sets of items in your stash to make selling for chaos more efficient.

Other stuff it can do:
- Check a tab for quality items for the 40% recipes, and highlight if found.
- Check a set of dump tabs for interesting rare items. Where interesting can be defined by a set of item rules. It also checks for items with matching names for the chance recipe. Since multiple tabs are checked, results are not highlighted.

~~Compared to poe_qol, this tool has no UI. So if that matters, use poe_qol.~~ (Not being maintained.)

If you need support for multiple dump tabs or the exalted shard recipe, please look at [EnhancePoEApp](https://github.com/kosace/EnhancePoEApp).


#### Table of contents
[TLDR to use it](#h01)<br>
[How to use it](#h02)<br>
[Automatic stuff](#h03)<br>
[Commands and hotkeys](#h04)<br>
[About the template and filter](#h05)<br>
[Security](#h06)<br>
[Configuration details](#h07)<br>
[Item rules](#h08)<br>
[Troubleshooting](#h09)<br>
[Building the tool](#h10)<br>
[Questions](#h11)<br>
[Links](#h12)<br>
[Donation](#h13)<br>

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
    - If you use the steam client but also have the standalone client installed, run the tool with a `steam` argument.
- In Path of Exile:
    - Change zones once for the tool to know where you are.
    - If this is your first time or if you have switched leagues, force an initial generation of the loot filter by typing an 'f' in the tool window.
    - Go to `Options->UI` and select "Chaos Helper" as your loot filter.
- As you play, reload your filter via `Options->Game` when it tells you to.
- When you are ready to sell a batch of recipes:
    - Use the highlight items hotkey to highlight the next batch of things to sell.
    - Use the highlight colors to guide you in Ctrl-clicking items from your stash to your inventory, so the bigger items move first.
    - Use the highlight items hotkey again to highlight the next set.
    - When nothing highlights, you are done for now.
- If you tend to dump everything you find into your quad tab, use the show junk items hotkey to highlight non-recipe items.

## Automatic stuff
<a name="h03" />

The Chaos Helper tool can do the following automatically:

- Given your account and poesessid, it can:
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
- When highlighting items for the recipe, it can:
    - Deal with 2hd weapons as well as 1x3 1hd weapons. The loot filter code will include 2x3 bows when weapons are highlighted, and it will make recipes with one 2x4 weapon since that will fit in the character inventory.
    - Do IDed sets as well as un-IDed sets. An IDed set must have both rings IDed, and it prefers IDed items for the other slots.
    - Optimise recipes to give chaos orbs instead of regal orbs. It does this by making sure that the items for at least one slot are ilvl 60-74 and then preferring ilvl 75+ for the other slots. You can take advantage of this by saving lots of ilvl 60-74 belts and amulets.

## Commands and hotkeys
<a name="h04" />

These are the commands you can send to the tool:

1. Pause (P): Pauses getting data from the PoE site automatically on area change.
2. Highlight items (H): (town only) To highlight sets of items to sell (it tries for two sets).
3. Show quality items (Q): (town only) To highlight sets of quality gems/flasks/maps to sell. It tries for one set of each. Only available if `qualityTab` is set in settings.jsonc. 
4. Show junk items (J): (town only) To toggle showing items in your stash tab that are not for the recipe, so you can clear them out.
5. Force update (F): To force a re-read of the stash tab and a write of the filter file.
6. Character check (C): Recheck your character and league. (This should happen automatically when you switch chararacters.)
7. Test pattern (T): (town only) Toggle displaying a test pattern to verify the stash window size is correct. Once good, you can disable the hotkey for it, until you change monitors.
8. Re-read configuation (R): Re-reads the settings.jsonc file.
9. Currency list (Z): Get currency prices from Poe Ninja, then print a listing of the contents of the currency tab in the ChaosHelper console window, with a total value. The listing should be copy-pastable into a spreadsheet.
10. Check dump tabs (D): Check dump tabs for interesting items (see "Item rules" below) and for items with matching names for chance recipe. Must have `dumpTabs` configured in settings.jsonc. 
11. Check item from clipboard (S): After doing Ctrl-C on an item in PoE, this command will check it against configured item rules (see "Item rules" below). Mostly useful for testing rules.
12. Close TCP ports (none): Close the TCP ports currently being used by the PoE process. This will cause an instant logout. Requires running ChaosHelper as an Admin.

The commands can be invoked:

1. By typing the appropriate letter (P,H,Q, etc.) in the ChaosHelper console window.
    - Best for commands you will hardly ever use, like character check and test pattern.
2. By using global hotkeys created by ChaosHelper
    - Hotkeys are only available for certain commands:
        - Highlight items: `Alt-H`
        - Show quality items: `Alt-K`
        - Show junk items: `Alt-J`
        - Force update: `Alt-F`
        - Character check: `Alt-C` but disabled by default
        - Test pattern: `Alt-T` but disabled by default
        - Close Ports: `Alt-Q` but disabled by default (and requires running as Admin)
    - These hotkeys can be rebound or disabled in settings.jsonc by commenting them out.
    - When defining a hotkey, for modifiers, use '^' for Ctrl, '+' for Shift and '!' for Alt.
    - For a list of key names/numbers, see https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.keys
    - Remember, these set **global** hotkeys, so be careful not to bork your other programs.
    - Other commands do not have hot keys. They must be invoked from the ChaosHelper console window or by sending a key to the ChaosHelper console. 
3. If you use the PoE-TradeMacro AutoHotkey script, there are scripts in the `AhkMacros` folder you can copy into your `Documents\PoE-TradeMacro\CustomMacros` folder.
    - Since PoE-TradeMacro binds some keys, the scripts come set up to be invoked using `Crtl-H`, none, `Ctrl-J`, `Ctrl-U`, `Ctrl-S` and `Ctrl-P`, respectively.
    - In this case, comment out the hotkeys in setting.jsonc.
    - This method can be used to invoke other commands in the ChaosHelper console

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

By default, the tool currently looks for a line containing the text "Override 270" in the template file.
For Neversink filters, this seems to put the chaos recipe section in a good place.
(Another value that seems to work is "Having this list too long".)

**NOTE:** Make sure the text specified in `filterMarker` only occurs once in the filter template, at the place you want the chaos recipe code to go.
For example, when using a Neversink filter in 3.14 it would be tempting to use something like "[[2300]] Endgame - Rare - Gear - T4 - rest" as the `filterMarker'.
However that text occurs *twice* in the filter - once in the place we want the code and once in the table of contents at the top of the file.
So using that text would cause the chaos recipe code to be inserted in the filter in both places.
This would probably not be what you want, for example, it would override the normal highlight for rare 6-socket armor.
As of 3.14, "Override 270" seems to look like a reasonable marker.

## Security
<a name="h06" />

I suggest building the tool yourself (see "Building the tool" below).
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

__`filter`__ ("ChaosHelper") is the name of the .filter file to create/update - just don't make it the same as `template`.
See "About the template and filter" for more info.

__`filterMarker`__ sets the text in the template file that will mark where to put the chaos recipe section.
See "About the template and filter" for more info.

__`filterAutoReload`__ (false) if true will send "{Enter}/itemfilter <filter name>{Enter}" when the filter updates to auto reload it.


__`filterDisplay`__ ({ "fontSize": 0, "text": "106 77 255", "back": "70 70 70", "border": "106 77 255" })
sets the the appearance for highlighted items in the updated filter.
Colors can be specified as a hex number like "0x4600e6" or a filter file color like "70 0 230".

__`soundFileVolume`__ (50) sets the volume of the sound alert for when the filter is updated. It can be an integer from 0 to 100.

__`maxSets`__ (12) is the number of chaos recipe sets the tool will aim to collect in your stash tab.
It uses this number to determine when to stop showing item classes in the loot filter.
A quad tab can hold about 16 complete sets (and a regular tab about 4).
Since you want to be able to ctrl-click to quickly dump items into the tab (and the tab can get messy over time - this is "fragmentation") setting this to 10 or 12 seems reasonable.

__`minIlvl`__ (60) is the minimum ilvl of items to highlight in the loot filter.
Setting this to 1 turns the tool into a Chance recipe helper...

__`maxIlvl`__ (-1) is the maximum ilvl of items to highlight in the loot filter.
It can be set to 74 to not show regal recipe items.
The default of -1 means to give the recipe highlight to all applicable items of ilvl 60 and above.
Note that setting `maxIlvl` will not prevent the filter from showing any rares that it would have shown before.
Only the highlighting may change from the chaos recipe highlight to what was already in the filter.
 
__`allowIDedSets`__ (true) sets whether the tool will support making sets including IDed items.
- I do this because I ID two-stone rings while levelling.
- Also has the filter show IDed rare rings and amulets, like breech rings.
- In an IDed set, both rings must be IDed, and other slots will prefer IDed items.
- The tool will not mix an IDed set and an un-IDed set in one sale.
- Can set to false to not show IDed rare rings and amulets in the  filter and to consider IDed items in the stash tab as junk.

__`chaosParanoiaLevel`__ (0) sets the level of effort to maximize chaos vs. regal orbs. It is a set of flag values, added together.
- The default of 0 will do the normal level of optimization.
- Adding 1 to the value will allow the tool to highlight a single set at a time.
For example, if the stash contains one ilvl 74 glove and one ilvl 74 helmet and everything else is ilvl 75+, it will highlight them in two individual sales.
- Adding 2 to the value will cause unidentified ilvl 75+ items to be favored over identified ilvl 60-74 items in IDed recipes.
This will tend to cause hoarding of identified ilvl 60-74 items in the stash tab.
- Adding 4 to the value will cause IDed recipes to not care about ilvl.
This will tend to save ilvl 60-74 items for unIDed recipis.
- I am currently using 5 (1 + 4).

__`ignoreMaxSets`__ causes the specified item classes to ignore the `maxSets` setting.
For example, setting `ignoreMaxSets` to "Rings,Amulets" when `maxSets` is 12,
will cause the filter to keep highlighting rings and amulets even when there are 12 or more of them in the stash tab.
Possible item classes are BodyArmours, Helmets, Gloves, Boots, OneHandWeapons, Belts, Amulets, and Rings

__`ignoreMaxSetsUnder75`__ causes the specified item classes to ignore the `maxSets` setting for items with ilvl under 75.
For example, setting `ignoreMaxSetsUnder75` to "Belts" when `maxSets` is 12,
will cause the filter to keep highlighting belts with ilvl < 75 even when there are 12 or more of them in the stash tab, but not highlight belts of ilvl 75 or above in that case.
Possible item classes are BodyArmours, Helmets, Gloves, Boots, OneHandWeapons, Belts, Amulets, and Rings

__`ignoreMaxIlvl`__ causes the specified item classes to ignore the `maxIlvl` setting, if it is set.
For example, setting `ignoreMaxIlvl` to "Rings,Amulets" when `maxIlvl` is 74
will cause the filter to keep highlighting rings and amulets at higher ilvls.
Possible item classes are BodyArmours, Helmets, Gloves, Boots, OneHandWeapons, Belts, Amulets, and Rings

__`highlightColors`__ ([ "0xffffff", "0xffff00", "0x00ff00", "0x0000ff" ]) is an array of color values (as strings)
to specify stash highlight colours for armour/weapons, helmets/gloves/boots, belts, and rings/amulets, respectively.
There must be four strings in the array and they can be hex numbers ("0xRRGGBB") or loot filter colors ("RRR GGG BBB").

__`highlightItemsHotkey`__, __`showQualityItemsHotkey`__, __`showJunkItemsHotkey`__, __`forceUpdateHotkey`__, __`characterCheckHotkey`__, __`testPatternHotkey`__ and __`closePortsHotkey`__
can be set to enable global hotkeys to execute ChaosHelper commands.
If not defined, the hotkeys are not enabled.
See "Commands and hotkeys" for more info.

__`hookMouseEvents`__ (true) sets if mouse events should be hooked.
Setting this to true will let the tool detect when clicking through a highlighted item and remove the highlight.
Set to false if this causes any issues on your system.
	
I suggest leaving `league`, `character`, `tabName`, `tabIndex` and `isQuadTab` at the defaults.
This will cause to the tool to auto-determine the values, which is good when there are multiple leagues available.
In particular, `tabIndex` is difficult, because the same tab can change from league to league and can depend on if there are Remove-only tabs visible.

__`stashReadMode`__ (Normal) Can be set to `Normal`, `Record`, `Playback` or `Manual`.

The default of `Normal` gets stash tab data from the PoE site.
`Record` does the same, and also saves the data to files in to ChaosHelper folder.
`Playback` tries to read data from the files saved by Record before going to the PoE site.
Since the saved data may not be current, this mode is only useful for testing item rules without hitting the site a lot.

The `Manual` mode is in case GGG blocks non-site access to the inventory information due to server overload.
When manual mode is enabled, the console window will show a URL to open in your browser (the URL should already be copied to your clipboard).
The page should open as a JSON document. Select the entire document and copy to your clipboard. At that point, the tool should continue.

__`processName`__ defines the process name for the running game.
The tool tries to auto-determine this, looking for clients in this order:
- "PathOfExile"
- "PathOfExileSteam"
- "PathOfExileEGS"
- "PathOfExile_KG"

The tool simply finds the first matching process.
To run two copies of Path of Exile while using the tool on one copy, run them using two different clients, 
and configure `processName` for the process you want the tool to go track.

__`areaEnteredPattern`__ ("] : You have entered ") is the text marker used when tailing client.txt to determine when a character change areas.
Translate this if you use the PoE client in a language other than English.

__`townZones`__ defines the areas that are considered to be "towns".
The commands noted as "town only" - highlight items, show junk items and test pattern - will only work when the tool thinks you are in a town zone.
If no town zones are defined, then the tool will treat every area as a town zone.
If you want protection from accidentally pressing one of the highlight hotkeys while in combat, then uncomment the list of zones, and add you hideout to the list.

__`currency`__ This array allows specifying minimum desired amounts for currencies and to put code in the loot filter to display them then the currency tab contains less than those amounts, if __`showMinimumCurrency`__ (false) is set to true.
This lets you run a stricter loot filter, but show certain currencies when the supply in your currency tab runs low. 
For example if there was an entry for wisdom scrolls that read:
```
{ "c": "Scroll of Wisdom", "value: 0, "desired": 100, "fontSize": 36, "text": "170 158 130 220", "border": "100 50 30 255", "back": "0 0 0 255" },
```
then when the loot filter was next written, if the number of wisdom scrolls in the currency tab was less than 100,
it would include a Show block for wisdom scrolls with the specified font size and colors.

- For the higlight feature to work the "desired", "fontSize", "text", "border" and "back" fields must all be specified.
- Since this feature is meant for lower value currencies that the loot filter might hide by default, it doesn't include a way to specify drop sounds, mini-map icons, etc.
- The default desired value of 0 disables making code blocks for that currency type.
- The loot filter will not be automatically re-written just to add/remove the currency blocks - use a force update in that case.
- This feature will never hide currency that the loot filter would display anyway, but may change the highlighting of it.

__`stashPageXYWH`__ ([ 0, 0, 0, 0 ]) specifies the rectangle in the PoE client window where the stash tab grid is.
It usually auto-determines correctly, but may need to be specified for certain monitors.

__`stashPageVerticalOffset`__ sets a number of pixels to vertically offset the stash rectangle.
To set this, type a 'T' in the ChaosHelper console window to toggle the test pattern and check how well the test pattern aligns with the stash tab squares.
If the pattern is a little too high, try increasing `stashPageVerticalOffset` (e.g. from 0 to 10) and typing 'R' to reload the configuation - or restart ChaosHelper.

If you need to set a custom value, take a screenshot of your stash tab,then open the screen shot in a program like IrfanView.
Make a select rectangle over the grid part of the tab, and determine the X,Y,Height,Width of the selection.
You can test the values by typing 't' in the ChaosHelper console to execute the test pattern command.

__`qualityTab`__ specifies a tab used to dump quality items for the 40% recipes.
For normal items, Only item with qualities between 1 and 19 will be considered, since a 20% quality normal item matches the recipe by itself.

__`qualityGemMapRecipeSlop`__ (0), `qualityFlaskRecipeSlop` (1) and `qualityScrapRecipeSlop` (3) specify how much slop to allow when making those recipes.
For example, a value of 2 would allow making recipes using ingredients with a total value of 40 to 42.
The tool will always try for exactly 40% total quality first.

## Item rules
<a name="h08" />

Item rules allow checking a set of dump tabs for interesting items.

An item is checked by converting the item's mods to a set of tags with values and then seeing if the tag values match any of a set of rules.

__Caveat__: The ChaosHelper tool only has access to the final mods of an item, not the actual affixes.
This means it can't tell if a mod is a combination of two affix, if there are open affix slots, etc.
So it can't do a perfect job, but it can be used as a way to note items for further inspection.
The moral is, if you want to allow for crafting, make your rules a little looser.

The `itemMods.csv` file defines the mods and what tags they affect. A mod can have multiple tags and a tag can have a multiplier. For example, if itemMods,csv contained these two mods:
```
+1 to Strength, AddStr, MaxLife*0.5
+1 to Maximum Life, MaxLife
```
then a ring with "+22 to Strength" and "+8 to Maximum Life" would generate tags of AddStr=22 and MaxLife=19.

Mods with two values - like "Adds 3 to 16 Cold Damage to Spells" will generate tags with an average of the two values (9.5 in this case).
The mods in itemMods.csv have all numerical values set to '1' to make sorting and checking for duplicates easier.
A mod line of "+13 to Strength" would work equivalently to "+1 to Strength".

The `itemRules.csv` file defines the rules to check. A rule is for a particular item class and specifies a set of terms to meet. For example, given the rules:
```
Super High Str, Rings, AddStr >= 20
Super High Life, Rings, MaxLife >= 20
Super High Life, Amulets, MaxLife >= 10
```
the ring above would only match the "Super High Str" rule, because the life isn't high enough for the first "Super High Life" rule and the base type is wrong for the second "Super High Life" rule.

Rules can have multiple terms, for example, a rule for decent boots might be:
```
Decent Boots, Boots, MoveSpeed >= 25, TotRes >= 75, MaxLife >= 60
```

Terms in rules can be sums of tag values (with optional multipliers), for example this rule might match a decent wand for a Shak Vortex build:
```
Vortex Weapon, OneHandWeapons, GemLvlCold*85 + DotCold*3.7 + SpellPctCold >= 100
```

## Troubleshooting
<a name="h09" />

1. You must configure your account name, poesessid and source filter in settings.jsonc.
3. If you are just starting out, type an 'f' in the ChaosHelper window to force an initial generation of the loot filter, and then change zones in-game.
4. Don't do a text selection in the ChaosHelper console window. If you do, the tool will be stopped until the selection is removed.
5. Check for error messages in the tool window. If the tool window has closed, there should be log files in the `logs` folder.
6. If you are playing in an non-English PoE, will need to build the tool yourself and translate some strings - particularly the "You have entered" it looks for in client.txt. (But tell me what it took, and I may make this easier in the future.)

## Building the tool
<a name="h10" />

Building the tool is pretty easy once you clone the git repo:
- It builds in Visual Studio 2019 Community Edition
- The code is all C#.
- You should be able to open ChaosHelper.sln and then build/run the tool
    - Still need to configure setting.jsonc, though.

## Potential questions
<a name="h11" />

**What does the filter do when you have enough of an item slot? Are they hidden entirely?**

ChaosHelper will never hide items your filter would show anyway.
It only inserts a set of SHOW blocks for the categories of items you don't have enough of in your dump tab.
But since PoE filtering stops on the first matching SHOW/HIDE block, you want the inserted block to come after your blocks for highlighting awesome items.
Wouldn't want a 6-link chest piece to just display as ho-hum-another-recipe-ingredient.

**Is it a separate filter, or something like a block you can put into any filter?**

The way you use it is that you point it at your filter, and configure a thing to say where in the filter the chaos recipe highlight code should go.
It does not alter your existing loot filter.
It copies your filter into "Chaos Helper.filter", inserting the chaos recipe code in the marked place.
By default it's set up to work with a Neversink filter - I found a place that had unique text that worked.
If you are using your own filter, I'd suggest putting a comment line like "#%%" or something unique in the filter in the place you want to chaos recipe code to be, and then configure the "filterMarker" setting to be "%%"

**I put a bunch of two handed axes in the tab and it tells me I can only make one recipe!**

Yep. It's an odd corner case. Only one recipe set using a 2x4 weapon will fit in your inventory at a time.
So the tool only considers 2x4 weapons based on the number of shorter weapons available.
When there are no shorter weapons, it will say one recipe, repeatedly.

**I'm running the Steam client and the tool is not detecting when I change zones.**

The tool favours using the stand alone client by default.
So if both PoE clients are installed, try starting the tool with a `steam` argument to force use of the steam client for process detection.
The `processName` setting may also have to be configured.

**I have some custom filter text I'd like to insert into the generated filter.**

For example, I am using a Neversink filter, but I'd like to insert lines to highlight items to craft for a Shak Vortex build. Put the custom text into a file named `filter_insert.txt` and put that file into the same folder as ChaosHelper.exe.
The contents of filter_insert.txt will be inserted into the generated filter just before the chaos recipe entries.
Note that `Hide` entries in filter_insert.txt will take precedence over entries later in the filter file.

**What's this about closing ports?**

Some players who play in the hardcore leagues use a "logout macro" as a panic button when they think their character is about to die and want to disconnect as quickly as possible.
The belief is that force closing the TCP ports used by PoE is the fastest way to do this.

The ClosePortsForPid utility in this repo can close all the TCP entries for a process.
Beyond that, it can directly close a specified TCP entry - doing so in a single Windows API call.

When port closing is enabled, ChaosHelper checks what TCP entry PoE is using whenever a new area is entered.
(PoE uses a different entry for each area instance.)
On executing the closePorts hotkey, it will try to directly close that TCP entry, with a fallback to closing all ports for the PoE process.

Using the SetTcpEntry API requires running ChaosHelper as Administrator. If you are going to do this, I strongly recommend building the binaries yourself.

## Links
<a name="h12" />

- Originally inspired by [poe_qolV2](https://github.com/notablackbear/poe_qolV2), which is in Python. (but no longer maintained)
- The overlay code comes from [Overlay.NET](https://github.com/lolp1/Overlay.NET). I copied in this code because there are posts in the project that the published Nuget package is not up to date with source code.
- The hotkey code is originally from [StackOverflow](https://stackoverflow.com/questions/3654787/global-hotkey-in-console-application/3654821).
- Using `base_items.json` from the [RePoE project](https://github.com/brather1ng/RePoE) to map item base types to categories. (At some point, I should grab the data from the ggpk file, like they do.)
- Using [GregsStack/InputSimulatorStandard](https://github.com/GregsStack/InputSimulatorStandard) for send chat commands to PoE.
- And it all started with [POE-TradeMacro](https://github.com/PoE-TradeMacro/POE-TradeMacro)

## Donation
<a name="h13" />

If this project helped you, you can help me :) 

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=XE5JR3FR458ZE&currency_code=USD)
