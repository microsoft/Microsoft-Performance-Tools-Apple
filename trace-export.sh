#!/bin/bash

#Init Variables
input_file=""

#Parse the arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --input) input_file="$2"; shift ;;
        *) echo "Unknown parameter: $1"; exit 1;;
    esac
    shift
done

if [[ -z "$input_file" ]]; then
    echo "Usage: $0 --input <input_file>"
    exit 1
fi

echo "Input file is: $input_file"

#if [[ ! -f "$input_file" ]]; then
#    echo "Error: $input_dir is not a valid file."
#    exit 1
#fi

extension="${input_file##*.}"

#if [[ "$extension" == "trace" ]]; then 
#    echo "This is not a trace file"
#fi

base_name="${input_file%.*}"
tmp_file="${base_name}.tmp"
output_file="${base_name}.xml"
echo "Output file is: $output_file"

xctrace export --input $input_file --xpath '/trace-toc/run[@number="1"]/data/table[@schema="time-profile"]' > $output_file

schemas=(
    "thread-state"
    "device-thermal-state-intervals"
    "syscall"
    "virtual-memory"
    "potential-hangs"
    "cpu-profile"
    "metal-gpu-intervals"
    "display-vsyncs-interval"
)

# Loop through each schema and export data
for schema in "${schemas[@]}"; do
    xctrace export --input "$input_file" --xpath "/trace-toc/run[@number='1']/data/table[@schema='$schema']" > "$tmp_file"
    sed '1d' "$tmp_file" >> "$output_file"
done

# Optionally, clean up the temporary file
rm "$tmp_file"


