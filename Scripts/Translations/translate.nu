print "DeepLX Translation Tool"
print ""

cd $env.FILE_PWD

let docker = (do { docker ps } | complete)

if $docker.exit_code != 0 {
  print "Docker is not running!"
  print "Please start Docker Desktop first."
  input "Press Enter to continue..."
  exit 1
}

try {
  dotnet run
  print ""
  print "Translation complete!"
} catch {
  print ""
  print "Translation failed!"
  input "Press Enter to continue..."
  exit 1
}

input "Press Enter to continue..."
