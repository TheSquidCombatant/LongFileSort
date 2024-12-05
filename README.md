# Problem statement
The input is a large text file, where each line is of the form "Number. String". For example:
```
415. Apple
30432. Something something something
1. Apple
32. Cherry is the best
2. Banana is yellow
```
Both parts can be repeated within the file. You need to get another file at the output, where all the lines
are sorted. Sorting criterion: the first part of String is compared first, if it matches, then Number.
Those in the example above it should be:
```
1. Apple
415. Apple
2. Banana is yellow
32. Cherry is the best
30432. Something something something
```
You need to write two programs:
1. A utility for creating a test file of a given size. The result of the work should be a text file of the type
described above. There must be some number of strings with the same String part.
2. The actual sorter. An important point, the file can be very large. The size of ~100Gb will be used for
testing.

When evaluating the completed task, we will first look at the result (correctness of generation/sorting
and running time), and secondly, at how the candidate writes the code. Programming language: C#.
# Solution structure
The solution implies support for sorting very large files with very long entries using a minimum amount of RAM.
## Creator
Executable file project that generates files with unsorted data. To run, you need to provide a configuration file of the following type:
```json
{
  "SourceFilePath": "source_100_mb_long_strings.txt",
  "SourceEncodingName": "UTF-8",
  "SourceSizeBytes": 107374182,
  "SourceOutputWithBom": false,
  "ProcessingTemporaryFolder": "temp",
  "NumberPartDigits": "0123456789",
  "NumberPartLength": 30,
  "NumberPartLengthVariation": 0,
  "StringPartSymbols": " abcdefghijklmnopqrstuvwxyz",
  "StringPartLength": 10737418,
  "StringPartLengthVariation": 0
}
```
To pass the configuration for execution, you need to put the configuration file named appsettings.json in the folder with the executable file.
You can also pass the path to the configuration file as the first parameter at startup. Preset configuration files for Creator are located in
the [`Presets\Creator`](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets/Creator) directory.
## Creator.Checker
Executable file project that checks generated source files with unsorted data. To run, you need a configuration file the same as for the Creator project.
## Sorter
Executable file project that creates a file with sorted data based on a file with unsorted data. To run, you need to provide a configuration file of the following type:
```json
{
  "SourceFilePath": "source_100_mb_long_strings.txt",
  "SourceEncodingName": "UTF-8",
  "TargetFilePath": "target_100_mb_long_strings.txt",
  "ProcessingTemporaryFolder": "temp"
}
```
To pass the configuration for execution, you need to put the configuration file named appsettings.json in the folder with the executable file.
You can also pass the path to the configuration file as the first parameter at startup. Preset configuration files for Creator are located in
the [`Presets\Sorter`](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets/Sorter) directory.
## Sorter.Checker
Executable file project that checks generated result files with sorted data. To run, you need a configuration file the same as for the Creator project.
## Utilities
Library project that implements all logic.
## Presets
Predefined load profile [configurations](https://github.com/TheSquidCombatant/LongFileSort/tree/main/Presets).
