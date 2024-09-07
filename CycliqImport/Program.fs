namespace CycliqTools.CycliqImport

open System
open System.IO
open System.Reflection

module Constants =
    let programName = "CycliqImport"
    let version = 
        Assembly.GetExecutingAssembly().GetName().Version.ToString()

module FileSystem =
    let getFiles (directory: string) (pattern: string) =
        Directory.GetFiles(directory, pattern, SearchOption.AllDirectories)

    let ensureDirectoryExists (path: string) =
        Directory.CreateDirectory(path) |> ignore

    let fileExists (path: string) =
        File.Exists(path)

    let getFileSize (path: string) =
        FileInfo(path).Length

module CycliqDrive =
    let findDrive () =
        DriveInfo.GetDrives()
        |> Array.tryFind (fun d ->
            d.IsReady && d.VolumeLabel.StartsWith("FLY", StringComparison.OrdinalIgnoreCase))

    let getCameraType (drive: DriveInfo) =
        if drive.VolumeLabel.StartsWith("FLY6", StringComparison.OrdinalIgnoreCase) then
            "Cycliq Fly6"
        else
            "Cycliq Fly12"

module Importer =
    let createTargetPath (baseDir: string) (creationTime: DateTime) (cameraType: string) =
        Path.Combine(baseDir, sprintf "%s %s" (creationTime.ToString("yyyyMMdd")) cameraType)

    let shouldCopyFile (sourcePath: string) (destPath: string) =
        not (FileSystem.fileExists destPath) ||
        FileSystem.getFileSize sourcePath <> FileSystem.getFileSize destPath

    let copyFile (sourcePath: string) (destPath: string) =
        if shouldCopyFile sourcePath destPath then
            printfn $"Importing %s{sourcePath} to %s{destPath}"
            File.Copy(sourcePath, destPath, true)
        else
            printfn $"Skipping %s{sourcePath} (already exists with same size)"

    let importFile (targetDir: string) (cameraType: string) (file: string) =
        let fileInfo = FileInfo(file)
        let targetPath = createTargetPath targetDir fileInfo.CreationTime cameraType
        FileSystem.ensureDirectoryExists targetPath
        let destPath = Path.Combine(targetPath, fileInfo.Name)
        copyFile fileInfo.FullName destPath

    let importFiles (sourceDrive: DriveInfo) (cameraType: string) (targetDir: string) =
        FileSystem.getFiles sourceDrive.RootDirectory.FullName "*.MP4"
        |> Array.iter (importFile targetDir cameraType)

module Program =
    let printUsage () =
        printfn $"Usage: %s{Constants.programName} <targetDirectory>"
        printfn $"Version: %s{Constants.version}"

    let tryImport targetDir =
        match CycliqDrive.findDrive() with
        | Some drive ->
            let cameraType = CycliqDrive.getCameraType drive
            Importer.importFiles drive cameraType targetDir
            printfn "Import completed successfully"
            0
        | None ->
            printfn "No Cycliq drive found"
            1

    [<EntryPoint>]
    let main argv =
        match argv with
        | [| "-h" | "--help" |] ->
            printUsage()
            0
        | [| targetDir |] ->
            tryImport targetDir
        | [||] ->
            printUsage()
            1
        | _ ->
            printfn "Invalid arguments."
            printUsage()
            1