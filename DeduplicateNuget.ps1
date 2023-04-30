

$ErrorActionPreference = 'Break'



function Get-PackageReferences($csprojXml)
{
    $packageReferences = $csprojXml.GetElementsByTagName("PackageReference")
    return $packageReferences
}

function Get-ProjectReferences($csprojXml)
{
    $projectReferences = $csprojXml.GetElementsByTagName("ProjectReference")
    return $projectReferences
}

function Test-IsSameReference($packageA, $packageB)
{
    if ($packageA.Include -eq $packageB.Include)
    {
        return $true
    }
}

function Test-ProjectContainsReference($projectXml, $reference)
{
    $elementsOfReferenceType = $projectXml.GetElementsByTagName($reference.Name)
    foreach ($element in $elementsOfReferenceType)
    {
        $isSame = Test-IsSameReference $reference $element
        if ($isSame -eq $true)
        {
            return $true
        }
    }

    return $false
}

function Test-ReferenceInParentProject($projectFile, $reference, $level)
{
    $projectFile = Get-Item $(Resolve-Path -Path $projectFile)

    Write-Host "$level-> Checking for" $reference.Include "in" $projectFile.Name
    [XML]$projectXml = Get-Content $projectFile
    $parentProjectReferences = Get-ProjectReferences $projectXml
    
    $parentContainsReference = Test-ProjectContainsReference $projectXml $reference
    if ($parentContainsReference -eq $true)
    {
        Write-Host "Duplicate found: " $reference.Include " in " $projectFile
        return $true
    }

    Push-Location $projectFile.Directory
    foreach ($parentProjectReference in $parentProjectReferences)
    {
        $parentContainsReference = Test-ReferenceInParentProject $parentProjectReference.Include $reference "$level-"
        if ($parentContainsReference -eq $true)
        {
            Write-Host "Duplicate found: " $reference.Include " in " $parentProjectReference.Include
            break
        }
    }
    Pop-Location | Out-Null

    return $parentContainsReference
}

function Test-HasChildNodes($xmlElement)
{
    # if there is no child nodes, then false
    if ($xmlElement.HasChildNodes -eq $false)
    {
        return $false
    }

    # Enumerate all children to determine any non whitespace item return true
    foreach ($childNode in $xmlElement.ChildNodes) {
        if ($childNode.LocalName -match "#whitespace")
        {
            continue
        }
        return $true
    }

    # has nodes, but all are whitespace, so no real child nodes
    return $false
}

function DeduplicatePackageReferences($projectXml)
{
    $projectReferences = Get-ProjectReferences $projectXml
    $packageReferences = Get-PackageReferences $projectXml

    foreach ($packageReference in $packageReferences) {
        foreach ($projectReference in $projectReferences)
        {
            $foundDuplicate = Test-ReferenceInParentProject $(Resolve-Path -Path $projectReference.Include) $packageReference
            if ($foundDuplicate)
            {
                Write-Host "Duplicate found: " $packageReference.Include
                $parentNode = $packageReference.ParentNode
                $parentNode.RemoveChild($packageReference) | Out-Null

                if ((Test-HasChildNodes $parentNode) -eq $false)
                {
                    $parentNode.ParentNode.RemoveChild($parentNode.PreviousSibling)
                    $parentNode.ParentNode.RemoveChild($parentNode) | Out-Null
                }
                
                $projectNeedsUpdate = $true
                break
            }
        }
    }

    return $projectNeedsUpdate
}

function DeduplicateProjectReferences($projectXml)
{
    $projectReferences = Get-ProjectReferences $projectXml

    for ($i = 0; $i -lt $projectReferences.Count; $i++) {
        Write-Host ""

        $projectReferenceToFind = $projectReferences[$i]
        for ($j = 0; $j -lt $projectReferences.Count; $j++) {
            if ($i -eq $j)
            {
                continue;
            }

            $projectToFindReferenceIn = $projectReferences[$j]
            Write-Host "For $($projectFile.Name): checking if $($projectReferenceToFind.Include) is in $($projectToFindReferenceIn.Include)"
            $foundDuplicate = Test-ReferenceInParentProject $projectToFindReferenceIn.Include $projectReferenceToFind ""
            if ($foundDuplicate) {
                $parentNode = $projectReferenceToFind.ParentNode
                $parentNode.RemoveChild($projectReferenceToFind) | Out-Null
                if ($parentNode.HasChildNodes -eq $false)
                {
                    $parentNode.ParentNode.RemoveChild($parentNode) | Out-Null
                }
                $projectNeedsUpdate = $true

                break;
            }
    
            Write-Host ".. not found"
        }
    }

    return $projectNeedsUpdate
}

function DoDependencyCleaning($projectFile)
{
    $projectFile = Get-Item $(Resolve-Path -Path $projectFile)

    [System.Xml.XmlDocument]$projectXml = New-Object System.Xml.XmlDocument;
    $projectXml.PreserveWhitespace = $true;
    $projectXml.Load($projectFile);
    
    Push-Location $projectFile.Directory
    $packagesNeedsUpdate = DeduplicatePackageReferences $projectXml
    #$projectsNeedsUpdate = DeduplicateProjectReferences $projectXml
    
    Pop-Location | Out-Null

    if (($packagesNeedsUpdate -eq $true) -or ($projectsNeedsUpdate -eq $true))
    {
        $projectXml.Save($projectFile)
    }
}


$projectFiles = Get-ChildItem .\src -Filter "*.csproj" -Recurse
foreach ($projectFile in $projectFiles) {
    DoDependencyCleaning $projectFile
}
