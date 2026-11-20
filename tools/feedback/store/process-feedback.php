<?php
declare(strict_types=1);
if(PHP_SAPI!=='cli'){http_response_code(403);exit;}
require_once __DIR__.'/feedback-queue.php';
try{
 $config=require __DIR__.'/config.php';$db=databaseOpen($config);supportSchema($db);privateLogSchema($db);
 echo json_encode(feedbackRun($db,$config),JSON_THROW_ON_ERROR).PHP_EOL;
}catch(Throwable $error){fwrite(STDERR,"Feedback queue could not be processed.\n");exit(1);}
