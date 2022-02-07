# tlp.sheets
Download data from Google Sheets in CSV format into your Unity3d project.

![Screenshot](https://github.com/thelastpointer/unitytimetracker/blob/master/Editor/DOCS/screenshot.png)

## What

This is an editor window; you copy and paste your Sheets URL, enter a local .CSV filename, and click on download. The tool handles an arbitrary number of sheets. Each sheet within a document needs to be a separate CSV (and has a separate URL too).

You then do whatever you want with the CSV data: typically, download them into a Resources folder, load them at runtime and have the CSV parser translate them into useful data for you.

```
private void Start()
{
	// Fetch the downloaded CSV
	var enemyCSV = Resources.Load<TextAsset>("enemies");
	// Convert CSV to useful data
	Enemy[] enemies = TLP.Sheets.CSVReader.Read<Enemy>(enemyCSV.text);
}
```

## Why

I was looking for an easy-to-use tool to handle data in bulk for my game. I already had a CSV parser, and Google Sheets data can be exported as CSV, so here it is.

You need this because you like simple stuff that does one thing only, for free, since you are an awesome person who gets shit done.

## How

A fairly undocumented feature, Google Sheets exposes an URL for your document that allows you to export data. You can read all about this on various StackOverflow questions. So we basically have to download data from an URL, and provide a nice interface for it!

My CSV parser is quite robust and is included in the project. It can create object arrays for you, or read into string arrays.

Some interesting things to note:
* The document list is saved into an Editor folder; this whole thing (apart from the CSV parser) is only usable in the Editor, so no runtime downloading! This is intentional, but easy to modify.
* Your sheet should be publicly available. Otherwise, Google will redirect us to a login page; my workaround was to disable redirections and print an error message to the console.

## Get

Use this repo as a Unity Package. Open the Package Manager, click the + button and select "Add package from git url...". Paste this repo.

Alternatively, you can download the project and just copy the .cs files, but then you'll have some garbage to clean up (like this readme, license, package definition, etc).
