# File Service




## File Service Uri Macros

### ${basedir}
`${basedir=String}`

The current application domain's base directory.

Example:
`${basedir}\file.txt`


### ${workdir}
`${basedir=String}`

Example:
`${basedir}\file.txt`


### ${workfolder}
`${basedir}` alias

${inputdir}
${inputfile}

${date}
`${date:universalTime=Boolean:format=String:culture=Culture}`
Current date and time.

* universalTime - Indicates whether to output UTC time instead of local time. Default: False
* format - Date format. Can be any argument accepted by DateTime.ToString(format). Note that semicolons needs to escaped with a backslash.
* culture - Culture used for rendering.Culture



${shortdate} - The short date in a sortable format yyyy-MM-dd
`${shortdate:universalTime=Boolean}`


${longdate}
`${longdate:universalTime=Boolean}`

The date and time in a long, sortable format yyyy-MM-dd HH:mm:ss.mmm.


${ticks} - The Ticks value of current date and time.
`${ticks:universalTime=Boolean}`


${time} - The time in a 24-hour, sortable format HH:mm:ss.mmm
`${time:universalTime=Boolean}`

${counter}
`${counter:increment=Integer:sequence=String:value=Integer}`

A counter value (increases on each layout rendering)

* increment - Value to be added to the counter after each layout rendering.Integer Default: 1
* sequence - Name of the sequence. Different named sequences can have individual values.
* value - Initial value of the counter.Integer Default: 1


${guid} - Globally-unique identifier (GUID).


## Modifiers

${pad:padCharacter=Char:padding=Integer:fixedLength=Boolean}
	 
* padCharacter - Padding character. Char Default:
* padding - Number of characters to pad the output to. Positive padding values cause left padding, negative values cause right padding to the desired width.
* fixedLength - Indicates whether to trim the rendered text to the absolute value of the padding length. Boolean Default: False

Example:
```
${counter:padCharacter=0:padding=6} // will produce 000001
file${counter:padCharacter=0:padding=6}.txt // will produce file000001.txt
```

${replace:searchFor=String:wholeWords=Boolean:replaceWith=String:ignoreCase=Boolean:regex=Boolean}

* searchFor - Text to search for.
* wholeWords - Indicates whether to search for whole words. Boolean
* replaceWith - Replacement string.
* ignoreCase - Indicates whether to ignore case when searching. Boolean
* regex - Indicates whether regular expressions should be used when searching. Boolean

Example:
```
${shortdate:searchFor=-:replaceWith=_} // will produce 2015_01_01
file${shortdate:searchFor=-:replaceWith=_}.txt // will produce file2015_01_01.txt
```

		 
${lowercase:lowercase=Boolean}
${uppercase:uppercase=Boolean}