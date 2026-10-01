param([string]$Path)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
if (!$Path) {
    $latest = Get-ChildItem (Join-Path $projectRoot 'Logs/BowAnimation') -Filter 'bow-*.json' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (!$latest) { throw 'No bow animation capture found.' }
    $Path = $latest.FullName
}
$capture = Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json
function Get-QuaternionAngle($a, $b) {
    $dot = $a.x*$b.x + $a.y*$b.y + $a.z*$b.z + $a.w*$b.w
    $na = [math]::Sqrt($a.x*$a.x + $a.y*$a.y + $a.z*$a.z + $a.w*$a.w)
    $nb = [math]::Sqrt($b.x*$b.x + $b.y*$b.y + $b.z*$b.z + $b.w*$b.w)
    if ($na -eq 0 -or $nb -eq 0) { throw 'Invalid zero quaternion in capture.' }
    # Use the double overload: integer Math.Min would round away small angles.
    2.0 * [math]::Acos([math]::Min(1.0, [math]::Abs($dot / ($na*$nb)))) * 180.0 / [math]::PI
}
# Guard the diagnostic math against sign and numeric-overload mistakes.
$identity = [pscustomobject]@{x=0.0;y=0.0;z=0.0;w=1.0}
$quarterTurn = [pscustomobject]@{x=0.0;y=[math]::Sqrt(0.5);z=0.0;w=[math]::Sqrt(0.5)}
if ([math]::Abs((Get-QuaternionAngle $identity $quarterTurn)-90.0) -gt 0.0001) { throw 'Quaternion angle self-check failed.' }
$negativeIdentity = [pscustomobject]@{x=0.0;y=0.0;z=0.0;w=-1.0}
if ((Get-QuaternionAngle $identity $negativeIdentity) -gt 0.0001) { throw 'Quaternion sign self-check failed.' }

$rows = @(foreach ($frame in $capture.frames) {
    if (!$frame.layers.Count) { continue }
    $layer = $frame.layers[0]
    $dominant = $layer.currentClips | Sort-Object weight -Descending | Select-Object -First 1
    [pscustomobject]@{
        phase=$frame.phase; clip=$dominant.name; weight=$frame.actionWeight
        bow=($frame.actionClip -like '*BowShot*'); torso=$frame.torsoLeftLean
        parameterMismatch=([math]::Abs($layer.velocityX-$layer.animatorVelocityX) + [math]::Abs($layer.velocityY-$layer.animatorVelocityY))
        transition=$layer.inTransition; rootMotion=$frame.rootMotion
    }
})
$groups = @(foreach ($group in ($rows | Where-Object bow | Group-Object phase,clip)) {
    $lean = $group.Group.torso | Measure-Object -Average -Minimum -Maximum
    [pscustomobject]@{
        phaseAndDominantClip=$group.Name; frames=$group.Count
        torsoMeanDegrees=$lean.Average; torsoMinDegrees=$lean.Minimum; torsoMaxDegrees=$lean.Maximum
        minimumActionWeight=($group.Group.weight | Measure-Object -Minimum).Minimum
    }
})
# A paused, fully weighted vendor clip is the controlled condition: compare the
# same clip and sample time, rather than mixing draw/release into the analysis.
$held = @($capture.frames | Where-Object {
    $capture.version -lt 5 -and $_.phase -eq 'Hold' -and $_.actionClip -like '*BowShot*' -and
    $_.actionSpeed -eq 0 -and $_.actionWeight -ge 0.9999
})
$boneChanges = @()
if ($held.Count -gt 0) {
    $reference = $held[0]
    $held = @($held | Where-Object {
        $_.actionClip -eq $reference.actionClip -and
        [math]::Abs($_.actionTime-$reference.actionTime) -lt 0.00001
    })
    $boneChanges = @(foreach ($name in @('Hips','Spine','Chest','UpperChest','LeftHand','RightHand')) {
        $referenceBone = $reference.bones | Where-Object name -eq $name
        if (!$referenceBone) { continue }
        $localAngles = @(); $rootAngles = @()
        foreach ($frame in $held) {
            $bone = $frame.bones | Where-Object name -eq $name
            if (!$bone) { continue }
            $localAngles += Get-QuaternionAngle $referenceBone.localRotation $bone.localRotation
            $rootAngles += Get-QuaternionAngle $referenceBone.animatorSpaceRotation $bone.animatorSpaceRotation
        }
        [pscustomobject]@{
            bone=$name; comparedFrames=$localAngles.Count
            maxLocalAngleFromFirstHeldFrame=($localAngles | Measure-Object -Maximum).Maximum
            maxAnimatorSpaceAngleFromFirstHeldFrame=($rootAngles | Measure-Object -Maximum).Maximum
        }
    })
}
$anchored = @($capture.frames | Where-Object {
    $_.actionClip -like '*BowShot*' -and $_.bowAnchorWeight -ge 0.9999
})
$anchorSummary = if ($capture.version -ge 3) {
    [pscustomobject]@{
        fullyAnchoredFrames=$anchored.Count
        maxSpineTiltErrorDegrees=$(if ($capture.version -ge 4) { ($anchored.bowSpineTiltError | Measure-Object -Maximum).Maximum } else { $null })
        maxSpineReferenceErrorDegrees=($anchored.bowSpineReferenceError | Measure-Object -Maximum).Maximum
        meanSpineReferenceErrorDegrees=($anchored.bowSpineReferenceError | Measure-Object -Average).Average
    }
} else { $null }
[pscustomobject]@{
    anchor=$anchorSummary
    file=(Resolve-Path -LiteralPath $Path).Path; captureUtc=$capture.utc
    totalFrames=$capture.frames.Count; heldComparisonFrames=$held.Count
    maxParameterMismatch=($rows.parameterMismatch | Measure-Object -Maximum).Maximum
    controllerTransitionFrames=@($rows | Where-Object transition).Count
    rootMotionFrames=@($rows | Where-Object rootMotion).Count
    bowGroups=$groups; heldBoneChanges=$boneChanges
    caveats=@('Version 5 Hold is an authored looping take; fixed-clock bone comparison is disabled.',
        'Version 4 corrects tilt only; full quaternion reference error is not a success criterion.',
        'Group means are frame-weighted, not duration-weighted.',
        '66-degree-style angle measurements are total 3D rotation, not lateral lean.',
        'Version 1 actionWeight may be stale with no action connected; only active BowShot frames are analyzed.',
        'Capture-start mask metadata can describe a different weapon if equipment changed.')
} | ConvertTo-Json -Depth 6
