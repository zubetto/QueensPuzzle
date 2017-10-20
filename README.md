<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/QueensPuzzle/master/Solutions_pano01header.jpg"/>
</p>

# n-Queens [Completion] problem solver
The class _QPSDamDetect_ contains n-Queens [Completion] problem solver implemented as multithreaded randomized non-recursive backtracking algorithm with dam-pruning
  
The randomization stands for shuffling of the array of free rows before start of the solving and helps to avoid of "dead" patterns.
  
The dam stands for a column or row which are not occupied by any queen yet, 
but at the same time queen placed on any cell of that column or row will be under attack. With sequentially filling of at least one coordinate (columns in this case) the dam detection allows eliminate a searching branch in advance (in the pic. below, the next positions (38,21); (38,31); (38,35) are allowed but position (37,40) is rejected as it causes formation of the dam)  

<p align="left">
  <img src="https://raw.githubusercontent.com/zubetto/QueensPuzzle/master/QPS_DamDetect_L50.jpg"/>
</p>
