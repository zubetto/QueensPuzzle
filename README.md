# QPSDamDetect
## n-Queens [Completion] problem solver implemented as multithreaded randomized non-recursive backtracking algorithm with dam-pruning 
The randomization stands for shuffling of the array of free rows before start of the solving and helps to avoid of "dead" patterns.
  
The dam stands for a column or row which are not occupied by any queen yet, 
but at the same time queen placed on any cell of that column or row will be under attack. With sequentially filling of at least one coordinate 
(columns in this case) the dam detection allows eliminate a searching branch in advance (in the pic. below, the next positions are allowed)
