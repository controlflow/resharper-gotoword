$config = 'Debug'
$nuspec_file = 'ReSharper.GoToWord.nuspec'
$package_id = 'ReSharper.GoToWord'

nuget pack $nuspec_file -Exclude 'ReSharper.GoToWord\bin.R8*\**' -Properties "Configuration=$config;ReSharperDep=ReSharper;ReSharperVer=[1.0];PackageId=$package_id.R90"