#!/bin/bash

# Check if enough arguments were provided
if [ $# -lt 2 ]; then
  echo "Usage: $0 <string_to_remove> <file_extension>"
  echo "Example: $0 _copy .png.meta"
  exit 1
fi

string_to_remove="$1"
file_extension="$2"

# Loop through all matching files in the current directory
for file in *"$file_extension"; do
  # Skip if no files match
  [ -e "$file" ] || continue

  # Remove the given string from the filename
  newname="${file//$string_to_remove/}"

  # Rename the file only if the name changed
  if [ "$file" != "$newname" ]; then
    mv -v -- "$file" "$newname"
  fi
done

echo "✅ Done! Removed '$string_to_remove' from filenames ending with '$file_extension'."
