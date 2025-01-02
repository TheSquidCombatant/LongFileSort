# Problem statement
The input is a large text file, where each line is of the form "Number. String". For example:
```
415. Apple
30432. Something something something
1. Apple
32. Cherry is the best
2. Banana is yellow
```
Both parts can be repeated within the file. You need to get another file at the output, where all
the lines are sorted. Sorting criterion: the first part of String is compared first, if it matches,
then Number. Those in the example above it should be:
```
1. Apple
415. Apple
2. Banana is yellow
32. Cherry is the best
30432. Something something something
```
You need to write two programs:
1. A utility for creating a test file of a given size. The result of the work should be a text file
of the type described above. There must be some number of strings with the same String part.
2. The actual sorter. An important point, the file can be very large. The size of ~100Gb will be
used for testing.

When evaluating the completed task, we will first look at the result (correctness of
generation/sorting and running time), and secondly, at how the candidate writes the code.
Programming language: C#.
# Solution structure
The solution implies support for sorting very large files with very long entries using a minimum
amount of RAM and in a minimum amount of time.
## Creator
Executable file project that generates files with unsorted data. To run, you need to provide a
configuration file of the following type:
```json
{
  "SourceFilePath": "source_001_gb_test_strings.txt",
  "SourceEncodingName": "UTF-8",
  "SourceSizeBytes": 104857600,
  "SourceOutputWithBom": false,
  "ProcessingTemporaryFolder": "temp",
  "NumberPartDigits": "0123456789",
  "NumberPartLength": 10,
  "NumberPartLengthVariation": 5,
  "StringPartSymbols": " abcdefghijklmnopqrstuvwxyz",
  "StringPartLength": 10,
  "StringPartLengthVariation": 5
}
```
To pass the configuration for execution, you need to put the configuration file named
`appsettings.json` in the folder with the executable file.

`SourceFilePath` - specifies the fully qualified name of the file in the operating system into
which the generated content will be writtend.

`SourceEncodingName` - encoding used to write the output file correctly.

`SourceSizeBytes` - size of the file that should be generated.

`SourceOutputWithBom` - optional Unicode special character code whose appearance as a magic number
at the start of a text stream.

`ProcessingTemporaryFolder` - specifies the full name of the directory in the operating system
that will be used to store possible temporary files.

`NumberPartDigits` - a set of digits that will be used to generate the numeric part of the row.

`NumberPartLength` - target length of the numeric part of the row.

`NumberPartLengthVariation` - the maximum size of the random deviation of the length of the numeric
part of the row.

`StringPartSymbols` - a set of symbols that will be used to generate the string part of the row.

`StringPartLength` - target length of the string part of the row.

`StringPartLengthVariation` -  the maximum size of the random deviation of the length of the string
part of the row.

You can also pass the path to the configuration file as the first parameter at startup. Preset
configuration files for Creator are located in the
[`Presets\Creator`](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets/Creator)
directory.
## Creator.Checker
Executable file project that checks generated source files with unsorted data. To run, you need a
configuration file the same as for the Creator project.
## Sorter
Executable file project that creates a file with sorted data based on a file with unsorted data. To
run, you need to provide a configuration file of the following type:
```json
{
  "CacheSizeLimitMegabytes": 200,
  "SourceEncodingName": "UTF-8",
  "SourceFilePath": "source_001_gb_test_strings.txt",
  "ProcessingTemporaryFolder": "temp",
  "TargetFilePath": "target_001_gb_test_strings.txt"
}
```
To pass the configuration for execution, you need to put the configuration file named
`appsettings.json` in the folder with the executable file.

`CacheSizeLimitMegabytes` - limits the peak memory consumption for the file cache during sorting.
Increas this value to reduces the number of reads from the disk. It is worth scaling the cache size
depending on the number of lines in the file being sorted, and not depending on the total size of
the original file. Setting a value less than `20 MB` does not make much sense, because a comparable
amount of memory at program startup will be occupied by the thread pool infrastructure by default.

`SourceEncodingName` - encoding used to read the input file correctly.

`SourceFilePath` - specifies the fully qualified name of the file in the operating system whose
contents are to be sorted.

`ProcessingTemporaryFolder` - specifies the full name of the directory in the operating system
that will be used to store possible temporary files.

`TargetFilePath` - specifies the fully qualified name of the file in the operating system into
which the sorted content will be saved.

You can also pass the path to the configuration file as the first parameter at startup. Preset
configuration files for Sorter are located in the
[`Presets\Sorter`](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets/Sorter)
directory.
## Sorter.Checker
Executable file project that checks generated result files with sorted data. To run, you need a
configuration file the same as for the Creator project.
## Utilities
Library project that implements all logic.
## Presets
Predefined load profile
[configurations](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets).