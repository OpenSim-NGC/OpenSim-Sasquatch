<?php
/*
 * Copyright (c) 2007, 2008 Contributors, http://opensimulator.org/
 * See CONTRIBUTORS for a full list of copyright holders.
 *
 * See LICENSE for the full licensing terms of this file.
 *
*/

//This looks like its lifted from http://www.weberdev.com/get_example-4372.html  I'd contact the original developer for licensing info, but his website is broken.

class DB
{
	var $Host = C_DB_HOST;				// Hostname of our MySQL server
	var $Database = C_DB_NAME;			// Logical database name on that server
	var $User = C_DB_USER;				// Database user
	var $Password = C_DB_PASS;			// Database user's password
	var $Link_ID = 0;				// Result of mysqli_connect()
	var $Query_ID = 0;				// Result of most recent mysqli_query()
	var $Record	= array();			// Current mysqli_fetch_array()-result
	var $Row;					// Current row number
	var $Errno = 0;					// Error state of query
	var $Error = "";

	function halt($msg)
	{
		echo("</TD></TR></TABLE><B>Database error:</B> $msg<BR>\n");
		echo("<B>MySQL error</B>: $this->Errno ($this->Error)<BR>\n");
		die("Session halted.");
	}

	function connect()
	{
		if($this->Link_ID == 0)
		{
			$this->Link_ID = mysqli_connect($this->Host, $this->User, $this->Password, $this->Database);
			if (!$this->Link_ID)
			{
				$this->halt("Link_ID == false, connect failed");
			}
			// $SelectResult = mysqli_select_db($this->Database, $this->Link_ID);
			// if(!$SelectResult)
			// {
				// $this->Errno = mysqli_errno($this->Link_ID);
				// $this->Error = mysqli_error($this->Link_ID);
				// $this->halt("cannot select database <I>".$this->Database."</I>");
			// }
		}
	}

 	function escape($String)
 	{
 		return mysqli_escape_string($String);
 	}

	function query($Query_String)
	{
		$this->connect();
		$this->Query_ID = mysqli_query($Query_String,$this->Link_ID);
		$this->Row = 0;
		$this->Errno = mysqli_errno();
		$this->Error = mysqli_error();
		if (!$this->Query_ID)
		{
			$this->halt("Invalid SQL: ".$Query_String);
		}
		return $this->Query_ID;
	}

	function next_record()
	{
		$this->Record = @mysqli_fetch_array($this->Query_ID);
		$this->Row += 1;
		$this->Errno = mysqli_errno();
		$this->Error = mysqli_error();
		$stat = is_array($this->Record);
		if (!$stat)
		{
			@mysqli_free_result($this->Query_ID);
			$this->Query_ID = 0;
		}
		return $this->Record;
	}

	function num_rows()
	{
		return mysqli_num_rows($this->Query_ID);
	}

	function affected_rows()
	{
		return mysqli_affected_rows($this->Link_ID);
	}

	function optimize($tbl_name)
	{
		$this->connect();
		$this->Query_ID = @mysqli_query("OPTIMIZE TABLE $tbl_name",$this->Link_ID);
	}

	function clean_results()
	{
		if($this->Query_ID != 0) mysqli_freeresult($this->Query_ID);
	}

	function close()
	{
		if($this->Link_ID != 0) mysqli_close($this->Link_ID);
	}
}
?>