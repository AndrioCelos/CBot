<?php
    // To complete setup of this script, edit the links on lines 140 and 156 to suit your site.
    // After doing so, delete this block.
    echo <<<"ERRORPAGE"
<html>
    <head>
        <title>UNO scoreboard</title>
    </head>
    <body style="color: white; background-color: black; font-family: Calibri, Arial, sans-serif;">
        <div class="error" style="background-color: #FF8080; border: 1px solid red; color: #C00000;">
            The script has not been fully set up. If you are the site administrator, open <strong>index.php</strong> for more details.
        </div>
    </body>
</html>

ERRORPAGE;
    exit;
?>
<?php
    function sort_scoreboard($min, $max, $key, $dir) {
        // Sorts all or part of $list.
        // Parameters:
        //   $min: The inclusive minimum index of the sublist to sort.
        //   $max: The inclusive maximum index of the sublist to sort.
        //   $key: The field to sort on.
        //   $dir: 1 to sort ascending; -1 to sort descending.
        // Returns: null
        // We use the quicksort algorithm.

        global $list;

        if ($max <= $min) return;
        if ($max - $min == 1) {
            if (($dir > 0 && compare($list[$min][$key], $list[$max][$key], $key) ==  1) ||
                ($dir < 0 && compare($list[$min][$key], $list[$max][$key], $key) == -1)) {
                $swap = $list[$min];
                $list[$min] = $list[$max];
                $list[$max] = $swap;
            }
            return;
        }

        $pivot = $list[$max][$key];
        $index = $min;
        
        for ($i = $min; $i < $max; $i += 1) {
            if (($dir > 0 && compare($list[$i][$key], $pivot, $key) !=  1) ||
                ($dir < 0 && compare($list[$i][$key], $pivot, $key) != -1)) {
                if ($i != $index) {
                    // Swap this entry to the pointer position.
                    $swap = $list[$index];
                    $list[$index] = $list[$i];
                    $list[$i] = $swap;
                }
                ++$index;
            }
        }

        // Enter the pivot.
        $swap = $list[$index];
        $list[$index] = $list[$max];
        $list[$max] = $swap;

        // Recursively sort the sublists.
        sort_scoreboard($min, $index - 1, $key, $dir);
        sort_scoreboard($index + 1, $max, $key, $dir);
    }

    function compare($value1, $value2, $key) {
        // Compares two values using a method based on the sort key.
        // Parameters:
        //   $value1: The value to compare.
        //   $value2: The value to which to compate $value1.
        //   $key   : The sort key. If this equals "name", strcasecmp will be used.
        // Returns: 1 if $value1 > $value2; 0 if $value1 = $value2; -1 if $value1 < $value2.

        if ($key == "name") {
            $result = strcasecmp($value1, $value2);
            if ($result > 0) return  1;
            if ($result < 0) return -1;
            return 0;
        } else {
            if ($value1 > $value2) return  1;
            if ($value1 < $value2) return -1;
            return 0;
        }
    }

    function sort_link($lperiod, $key, $flip, $text) {
        // Creates a link with the given parameters.
        // Parameters:
        //   $lperiod: The period to use in the link.
        //   $key    : The sort key to use in the link. This also determines the default value for the direction.
        //   $flip   : If true, it indicates this is a column header, and should flip the sort order.
        //   $text   : The text to display to the user.
        // Returns: an <a> or <span> tag to be rendered.

        global $period, $sort, $dir;
        if (!$flip && $period == $lperiod) {
            return "<span class=\"selected\">$text</span>";
        }
        if ($sort == $key) {
            if ($flip) $newdir = -$dir; else $newdir = $dir;
            if ($newdir == 1)
                return "<a class=\"selected\" href=\"?period=$lperiod&sort=$key&dir=asc\">$text</a>";
            else
                return "<a class=\"selected\" href=\"?period=$lperiod&sort=$key&dir=desc\">$text</a>";
        } else {
            if ($key == "name" || $key == "losses")
                return "<a href=\"?period=$lperiod&sort=$key&dir=asc\">$text</a>";
            else
                return "<a href=\"?period=$lperiod&sort=$key&dir=desc\">$text</a>";
        }
    }

    // Parse the GET parameters.

    if (!isset($_GET["period"])) $period = "current";
    else if ($_GET["period"] == "current") $period = "current";
    else if ($_GET["period"] == "last") $period = "last";
    else if ($_GET["period"] == "alltime") $period = "alltime";

    if (isset($_GET["sort"])) $sort = $_GET["sort"];
    else $sort = "points";

    if ($sort == "name" || $sort == "losses") {
        if (isset($_GET["dir"]) && $_GET["dir"] == "desc") $dir = -1;
        else $dir = 1;
    } else {
        if (isset($_GET["dir"]) && $_GET["dir"] == "asc") $dir = 1;
        else $dir = -1;
    }
?>

<!DOCTYPE HTML PUBLIC "-//W3C//DTD HTML 4.01//EN" "http://www.w3.org/TR/html4/strict.dtd">
<html>
    <head>
        <title>UNO scoreboard</title>
            <!-- SETUP: Replace this link with the actual path to your stylesheet file. -->
            <link rel="stylesheet" href="/style/unostats.css">
            <!-- Generated by http://www.colorzilla.com/gradient-editor/ -->
            <!--[if gte IE 9]>
                <style type="text/css">
                    div.shadow {
                        filter: none;
                    }
                </style>
            <![endif]-->
        </head>
    <body>
<?php
    if (!$period) {
        echo "<div class=\"error\">Invalid <strong>period</strong> parameter.</p>\r\n";
    } else {
        // SETUP: Replace this path with your CBot working directory and output filename.
        $rawdata = file_get_contents("/home/andrio/cbot/UNO-stats.json");
        if ($rawdata === false) {
            echo "<div class=\"error\">Failed to load the scoreboard.</div>\r\n";
        } else {  
            $data = json_decode($rawdata, true);
            $list = $data[$period];
            if ($list === null) {
                echo "<div class=\"error\">Failed to parse the scoreboard.</div>\r\n";
            } else {
                if ($sort != "none")
                sort_scoreboard(0, count($list) - 1, $sort, $dir);
                echo <<<"HEAD"
            <div class="header">
            <h1>UNO Leaderboard</h1>
        
HEAD;
                echo "<p>Period: " . sort_link("current", $sort, false, "Current leaderboard") . " | " . sort_link("last", $sort, false, "Last leaderboard") . " | " . sort_link("alltime", $sort, false, "All time") . "</p>";
                echo <<<"HEAD"
            <div class="shadow"></div>
            <table cellspacing=0>
                <tr>

HEAD;
                echo "<th width= 48px>#</th>\r\n";
                echo "<th width=200px>" . sort_link($period, "name", true, "Player") . "</th>\r\n";
                echo "<th width= 60px>" . sort_link($period, "points", true, "Score") . "</th>\r\n";
                echo "<th width= 60px>" . sort_link($period, "plays", true, "Plays") . "</th>\r\n";
                echo "<th width= 60px>" . sort_link($period, "wins", true, "Wins") . "</th>\r\n";
                echo "<th width= 60px>" . sort_link($period, "losses", true, "Losses") . "</th>\r\n";
                echo "<th width= 60px>" . sort_link($period, "challenge", true, "Challenge") . "</th>\r\n";
                echo <<<"HEAD"
                </tr>
            </table>
        </div>
        <h1>UNO Leaderboard</h1>
        <p>Period: Current leaderboard | Last leaderboard | All time</p>
        <div class="shadow"></div>
            <table cellspacing=0>
                <tr>
                    <th width= 48px>#</th>
                    <th width=200px>Player</th>
                    <th width= 60px>Score</th>
                    <th width= 60px>Plays</th>
                    <th width= 60px>Wins</th>
                    <th width= 60px>Losses</th>
                    <th width= 60px>Challenge</th>
                </tr>

HEAD;
                $i = 0; $rank = 0;
                foreach ($list as $entry) {
                    ++$i;
                    if ($rank == 0 || $entry[$sort] != $check) {
                        $rank = $i;
                        $check = $entry[$sort];
                    }
                    echo "<tr>\r\n";
                    echo "<td>$rank</td>\r\n";
                    echo "<td class=\"name\">" . $entry["name"] . "</td>\r\n";
                    echo "<td>" . number_format($entry["points"], 0, ".", " ") . "</td>\r\n";
                    echo "<td>" . number_format($entry["plays"], 0, ".", " ") . "</td>\r\n";
                    echo "<td>" . number_format($entry["wins"], 0, ".", " ") . "</td>\r\n";
                    echo "<td>" . number_format($entry["losses"], 0, ".", " ") . "</td>\r\n";
                    echo "<td>" . number_format($entry["challenge"], 0, ".", " ") . "</td>\r\n";
                    echo "</tr>\r\n";
                }
                echo "</table>\r\n";
            }
        }
    }
    echo <<<FOOT
    </body>
</html>
FOOT;
?>
