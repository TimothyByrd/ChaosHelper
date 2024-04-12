# this is to reset settings.jsonc to default values before committing

# powershell -executionpolicy remotesigned -File clean_settings.ps1

$sourceDirectory = "."
$path = [System.IO.Path]::Combine($sourceDirectory, "settings.jsonc")
$origText = [System.IO.File]::ReadAllText($path)
$text = $origText
$text = $text -replace '"account"\s*:\s*"[^"]*"', "`"account`": `"`""
$text = $text -replace '"poesessid"\s*:\s*"[^"]*"', "`"poesessid`": `"`""
$text = $text -replace '"template"\s*:\s*"[^"]*"', "`"template`": `"`""

$text = $text -replace '"filter"\s*:\s*"[^"]*"', "`"filter`": `"ChaosHelper`""
$text = $text -replace '"filterMarker"\s*:\s*"[^"]*"', "`"filterMarker`": `"`""
$text = $text -replace '"soundFileVolume"\s*:\s*\d+\s*,', "`"soundFileVolume`": 25,"

$text = $text -replace '"maxSets"\s*:\s*\d+\s*,', "`"maxSets`": 10,"
$text = $text -replace '"minIlvl"\s*:[^,]+,', "`"minIlvl`": 60,"
$text = $text -replace '"maxIlvl"\s*:[^,]+,', "`"maxIlvl`": -1,"
$text = $text -replace '"allowIDedSets"\s*:[^,]+,', "`"allowIDedSets`": true,"
$text = $text -replace '"chaosParanoiaLevel"\s*:[^,]+,', "`"chaosParanoiaLevel`": 0,"
$text = $text -replace '"includeInventoryOnForce"\s*:[^,]+,', "`"includeInventoryOnForce`": false,"

$text = $text -replace '"ignoreMaxSets"\s*:\s*"[^"]*"', "`"ignoreMaxSets`": `"Rings,Amulets`""
$text = $text -replace '"ignoreMaxIlvl"\s*:\s*"[^"]*"', "`"ignoreMaxIlvl`": `"Rings,Amulets`""

$text = $text -replace '"[^"]*"\s*,\s*//\s*armour/weapons', "`"0xffffff`", // armour/weapons"
$text = $text -replace '"[^"]*"\s*,\s*//\s*helmets/gloves/boots', "`"0xffff00`", // helmets/gloves/boots"
$text = $text -replace '"[^"]*"\s*,\s*//\s*belts', "`"0x00ff00`", // belts"
$text = $text -replace '"[^"]*"\s*,\s*//\s*rings/amulets', "`"0x6060ff`", // rings/amulets"

$text = $text -replace '"highlightItemsHotkey"\s*:\s*"[^"]*"', "`"highlightItemsHotkey`": `"!H`""
$text = $text -replace '"showJunkItemsHotkey"\s*:\s*"[^"]*"', "`"showJunkItemsHotkey`": `"!J`""
$text = $text -replace '"forceUpdateHotkey"\s*:\s*"[^"]*"', "`"forceUpdateHotkey`": `"!F`""
# $text = $text -replace '(//\s*)?"characterCheckHotkey"\s*:\s*"[^"]*"', "//`"characterCheckHotkey`": `"!C`""
# $text = $text -replace '(//\s*)?"testModeHotkey"\s*:\s*"[^"]*"', "//`"testModeHotkey`": `"!T`""
$text = $text -replace '"enabled":\s*true\s*,\s*"key"', '"enabled": false, "key"'

$text = $text -replace '"league"\s*:\s*"[^"]*"', "`"league`": `"`""
$text = $text -replace '"character"\s*:\s*"[^"]*"', "`"character`": `"`""
$text = $text -replace '"tabName"\s*:\s*"[^"]*"', "`"tabName`": `"`""
$text = $text -replace '"tabIndex"\s*:[^,]+,', "`"tabIndex`": -1,"
$text = $text -replace '"isQuadTab"\s*:[^,]+,', "`"isQuadTab`": true,"
$text = $text -replace '"testMode"\s*:[^,]+,', "`"testMode`": `"Normal`","
$text = $text -replace '"stashReadMode"\s*:\s*"[^"]*"', "`"stashReadMode`": `"Normal`""

$text = $text -replace '"clientTxt"\s*:\s*"[^"]*"', "`"clientTxt`": `"`""
$text = $text -replace '"processName"\s*:\s*"[^"]*"', "`"processName`": `"`""

$text = $text -replace '"areaEnteredPattern"\s*:\s*"[^"]*"', "`"areaEnteredPattern`": `"] : You have entered `""

$text = $text -replace '"stashPageVerticalOffset"\s*:[^,]+,', "`"stashPageVerticalOffset`": 10,"

$text = $text -replace '"desired"\s*:[^,]+,', "`"desired`": 0,"

$text = $text -replace '"qualityTab"\s*:\s*"[^"]*"', "`"qualityTab`": `"`""
$text = $text -replace '"qualityScrapRecipeSlop"\s*:[^,]+,', "`"qualityScrapRecipeSlop`": 3,"
$text = $text -replace '"qualityGemRecipeSlop"\s*:[^,]+,', "`"qualityGemRecipeSlop`": 0,"
$text = $text -replace '"qualityFlaskRecipeSlop"\s*:[^,]+,', "`"qualityFlaskRecipeSlop`": 1,"
$text = $text -replace '"qualityVaalGemMaxQualityToUse"\s*:[^,]+,', "`"qualityVaalGemMaxQualityToUse`": 0,"

if ($origText -ne $text) {
    [System.IO.File]::WriteAllText($path, $text)
    Write-Output "Updated $path"
}
else
{
    Write-Output "$path already up to date"
}