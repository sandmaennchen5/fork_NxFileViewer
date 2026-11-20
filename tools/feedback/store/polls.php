<?php
declare(strict_types=1);
require_once __DIR__.'/database.php';
require_once __DIR__.'/poll-translations.php';
function pollQuery(PDO $db,string $sql,array $args=[]): PDOStatement {$q=$db->prepare($sql);$q->execute($args);return $q;}
function pollsSchema(PDO $db): void {
 databaseSqliteSchema($db,"CREATE TABLE IF NOT EXISTS polls (id INTEGER PRIMARY KEY,question TEXT NOT NULL,closed INTEGER NOT NULL DEFAULT 0,kind TEXT NOT NULL DEFAULT 'choice',show_in_app INTEGER NOT NULL DEFAULT 1,show_results_in_app INTEGER NOT NULL DEFAULT 1,notify_in_app INTEGER NOT NULL DEFAULT 1);
 CREATE TABLE IF NOT EXISTS poll_options (id INTEGER PRIMARY KEY,poll_id INTEGER NOT NULL,label TEXT NOT NULL,active INTEGER NOT NULL DEFAULT 1,position INTEGER NOT NULL DEFAULT 0);
 CREATE TABLE IF NOT EXISTS poll_votes (poll_id INTEGER NOT NULL,participant TEXT NOT NULL,option_id INTEGER NOT NULL,PRIMARY KEY(poll_id,participant));
 CREATE TABLE IF NOT EXISTS poll_responses (poll_id INTEGER NOT NULL,participant TEXT NOT NULL,kind TEXT NOT NULL,answer TEXT NULL,created_at INTEGER NOT NULL,PRIMARY KEY(poll_id,participant));
 CREATE TABLE IF NOT EXISTS poll_rate (bucket TEXT PRIMARY KEY,count INTEGER NOT NULL,expires INTEGER NOT NULL);
 CREATE TABLE IF NOT EXISTS poll_sequence (singleton INTEGER PRIMARY KEY CHECK(singleton=1),last_id INTEGER NOT NULL)");
 pollTranslationsSchema($db);
 $db->exec(databaseSql($db,'INSERT INTO poll_sequence (singleton,last_id) SELECT 1,COALESCE(MAX(id),0) FROM polls WHERE true ON CONFLICT(singleton) DO UPDATE SET last_id=MAX(last_id,excluded.last_id)',
 'INSERT INTO poll_sequence (singleton,last_id) SELECT 1,COALESCE(MAX(id),0) FROM polls WHERE true ON DUPLICATE KEY UPDATE last_id=GREATEST(last_id,VALUES(last_id))'));
}
function pollLock(PDO $db,int $id): array {
 $poll=pollQuery($db,'SELECT * FROM polls WHERE id=?'.(databaseIsMariaDb($db)?' FOR UPDATE':''),[$id])->fetch(PDO::FETCH_ASSOC);
 if(!$poll)throw new DomainException('Umfrage nicht gefunden.');return $poll;
}
function pollAnswers(string $value): array {
 $value=trim(str_replace("\r",'',$value));return $value===''?[]:array_map('trim',explode("\n",$value));
}
function pollValidateChoices(array $labels): void {
 if(count($labels)<2||count($labels)>20||in_array('',$labels,true)||count(array_unique($labels))!==count($labels)||max(array_map('strlen',$labels))>500)
 throw new DomainException('Frage und 2 bis 20 unterschiedliche Antworten eingeben.');
}
function pollFlag(array $input,string $key,bool $default=false): int {return isset($input[$key])?((string)$input[$key]==='1'?1:0):($default?1:0);}
function pollCreate(PDO $db,array $input): int {
 $question=trim((string)($input['question']??''));$kind=(string)($input['kind']??'choice');
 if($question===''||strlen($question)>1000||!in_array($kind,['choice','text'],true))throw new DomainException('Ungültige Frage oder Antwortart.');
 $labels=$kind==='choice'?pollAnswers((string)($input['options']??'')):[];if($kind==='choice')pollValidateChoices($labels);
 $english=pollEnglishInput((string)($input['question_en']??''),(string)($input['options_en']??''),count($labels));
 $defaults=!isset($input['visibility_form']);
 $db->beginTransaction();try{
  pollQuery($db,'UPDATE poll_sequence SET last_id=last_id+1 WHERE singleton=1');$id=(int)$db->query('SELECT last_id FROM poll_sequence WHERE singleton=1')->fetchColumn();
  pollQuery($db,'INSERT INTO polls (id,question,kind,show_in_app,show_results_in_app,notify_in_app) VALUES (?,?,?,?,?,?)',[$id,$question,$kind,pollFlag($input,'show_in_app',$defaults),pollFlag($input,'show_results_in_app',$defaults),pollFlag($input,'notify_in_app',$defaults)]);
  foreach($labels as $index=>$label)pollQuery($db,'INSERT INTO poll_options (poll_id,label,position) VALUES (?,?,?)',[$id,$label,$index]);
  pollSaveEnglish($db,$id,$english);$db->commit();return $id;
 }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}
function pollEdit(PDO $db,int $id,array $input): void {
 $db->beginTransaction();try{
  $poll=pollLock($db,$id);$question=trim((string)($input['question']??''));$questionEn=trim((string)($input['question_en']??''));
  if($question===''||strlen($question)>1000||strlen($questionEn)>1000)throw new DomainException('Ungültige Frage oder Antwortart.');
  $existing=pollQuery($db,'SELECT * FROM poll_options WHERE poll_id=? ORDER BY position,id',[$id])->fetchAll(PDO::FETCH_ASSOC);
  $changes=[];$english=[];$active=[];
  foreach($existing as $option){
   $key=(int)$option['id'];$de=trim((string)($input['option_de'][$key]??$option['label']));$en=trim((string)($input['option_en'][$key]??''));$enabled=isset($input['option_active'][$key]);
   if($de===''||strlen($de)>500||strlen($en)>500)throw new DomainException('Antworttexte dürfen nicht leer oder zu lang sein.');
   if($enabled){$active[]=$de;if($questionEn!==''&&$en==='')throw new DomainException('Englische Antworten für alle aktiven Auswahlen eingeben.');}
   $changes[]=[$de,$enabled?1:0,$key];$english[$key]=$en;
  }
  $new=$poll['kind']==='choice'?pollAnswers((string)($input['new_options']??'')):[];$newEn=pollAnswers((string)($input['new_options_en']??''));
  if($poll['kind']==='choice'){
   pollValidateChoices(array_merge($active,$new));
   if($newEn&&count($newEn)!==count($new))throw new DomainException('Die Anzahl der Antworten muss übereinstimmen.');
   if($questionEn!==''&&count($newEn)!==count($new))throw new DomainException('Englische Antworten für alle aktiven Auswahlen eingeben.');
   foreach($newEn as $label)if($label===''||strlen($label)>500)throw new DomainException('Antworttexte dürfen nicht leer oder zu lang sein.');
  }
  pollQuery($db,'UPDATE polls SET question=?,closed=?,show_in_app=?,show_results_in_app=?,notify_in_app=? WHERE id=?',[$question,pollFlag($input,'closed'),pollFlag($input,'show_in_app'),pollFlag($input,'show_results_in_app'),pollFlag($input,'notify_in_app'),$id]);
  foreach($changes as $change)pollQuery($db,'UPDATE poll_options SET label=?,active=? WHERE id=?',$change);
  foreach($new as $index=>$label){pollQuery($db,'INSERT INTO poll_options (poll_id,label,position) VALUES (?,?,?)',[$id,$label,count($existing)+$index]);$english[(int)$db->lastInsertId()]=$newEn[$index]??'';}
  pollQuery($db,"DELETE FROM poll_translations WHERE poll_id=? AND language='en'",[$id]);
  pollQuery($db,"DELETE FROM poll_option_translations WHERE language='en' AND option_id IN (SELECT id FROM poll_options WHERE poll_id=?)",[$id]);
  if($questionEn!==''){
   pollQuery($db,"INSERT INTO poll_translations (poll_id,language,question) VALUES (?,'en',?)",[$id,$questionEn]);
   foreach($english as $key=>$label)if($label!=='')pollQuery($db,"INSERT INTO poll_option_translations (option_id,language,label) VALUES (?,'en',?)",[$key,$label]);
  }
  $db->commit();
 }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}
function pollDelete(PDO $db,int $id): void {
 $db->beginTransaction();try{pollLock($db,$id);
  foreach(['poll_votes','poll_responses','poll_translations'] as $table)pollQuery($db,"DELETE FROM $table WHERE poll_id=?",[$id]);
  pollQuery($db,'DELETE FROM poll_option_translations WHERE option_id IN (SELECT id FROM poll_options WHERE poll_id=?)',[$id]);
  pollQuery($db,'DELETE FROM poll_options WHERE poll_id=?',[$id]);pollQuery($db,'DELETE FROM polls WHERE id=?',[$id]);$db->commit();
 }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}
function pollRespond(PDO $db,int $id,string $participant,string $bucket,string $action,?int $option=null,?string $answer=null,bool $app=false): void {
 if(!in_array($action,['vote','text','skip','unskip'],true))throw new DomainException('Unbekannte Aktion.');
 $db->beginTransaction();try{
  pollQuery($db,databaseSql($db,'INSERT INTO poll_rate (bucket,count,expires) VALUES (?,1,?) ON CONFLICT(bucket) DO UPDATE SET count=count+1','INSERT INTO poll_rate (bucket,count,expires) VALUES (?,1,?) ON DUPLICATE KEY UPDATE count=count+1'),[$bucket,time()+3600]);
  if((int)pollQuery($db,'SELECT count FROM poll_rate WHERE bucket=?',[$bucket])->fetchColumn()>30)throw new DomainException('Zu viele Versuche. Bitte später erneut versuchen.');
  $poll=pollLock($db,$id);
  if((int)$poll['closed']===1||($app&&!(int)$poll['show_in_app']))throw new DomainException('Diese Umfrage nimmt keine Antworten an.');
  $vote=pollQuery($db,'SELECT option_id FROM poll_votes WHERE poll_id=? AND participant=?',[$id,$participant])->fetchColumn();
  $response=pollQuery($db,'SELECT kind FROM poll_responses WHERE poll_id=? AND participant=?',[$id,$participant])->fetchColumn();
  if($action==='unskip')pollQuery($db,"DELETE FROM poll_responses WHERE poll_id=? AND participant=? AND kind='declined'",[$id,$participant]);
  elseif($vote!==false||$response!==false){/* Idempotent: the first response or refusal is retained. */}
  elseif($action==='skip')pollQuery($db,"INSERT INTO poll_responses (poll_id,participant,kind,created_at) VALUES (?,?,'declined',?)",[$id,$participant,time()]);
  elseif($action==='text'){
   $answer=trim($answer??'');if($poll['kind']!=='text'||$answer===''||strlen($answer)>8000||!preg_match('//u',$answer)||preg_match_all('/./us',$answer)>2000)throw new DomainException('Freitext mit höchstens 2000 Zeichen eingeben.');
   pollQuery($db,"INSERT INTO poll_responses (poll_id,participant,kind,answer,created_at) VALUES (?,?,'text',?,?)",[$id,$participant,$answer,time()]);
  }else{
   if($poll['kind']!=='choice'||pollQuery($db,'SELECT id FROM poll_options WHERE poll_id=? AND id=? AND active=1',[$id,$option])->fetchColumn()===false)throw new DomainException('Diese Antwort ist nicht verfügbar.');
   pollQuery($db,'INSERT INTO poll_votes (poll_id,participant,option_id) VALUES (?,?,?)',[$id,$participant,$option]);
  }
  $db->commit();
 }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
}
function pollsOverview(PDO $db,string $participant,string $language,bool $app=false): array {
 $rows=pollQuery($db,'SELECT * FROM polls ORDER BY id DESC')->fetchAll(PDO::FETCH_ASSOC);$polls=[];
 foreach($rows as $row){
  $closed=(bool)$row['closed'];$visible=(bool)$row['show_in_app'];$results=!$app||(bool)$row['show_results_in_app'];
  if($app&&!( (!$closed&&$visible)||(bool)$row['show_results_in_app']))continue;
  $id=(int)$row['id'];$options=pollQuery($db,'SELECT o.id,o.label,o.active,o.position,COUNT(v.option_id) AS votes FROM poll_options o LEFT JOIN poll_votes v ON v.poll_id=o.poll_id AND v.option_id=o.id WHERE o.poll_id=? GROUP BY o.id,o.label,o.active,o.position ORDER BY o.position,o.id',[$id])->fetchAll(PDO::FETCH_ASSOC);
  $selected=pollQuery($db,'SELECT option_id FROM poll_votes WHERE poll_id=? AND participant=?',[$id,$participant])->fetchColumn();
  $response=pollQuery($db,'SELECT kind,answer FROM poll_responses WHERE poll_id=? AND participant=?',[$id,$participant])->fetch(PDO::FETCH_ASSOC);
  $declined=($response['kind']??'')==='declined';$answered=$selected!==false||($response['kind']??'')==='text';$can=!$closed&&(!$app||$visible)&&!$answered&&!$declined;
  $count=(int)pollQuery($db,"SELECT COUNT(*) FROM poll_responses WHERE poll_id=? AND kind='text'",[$id])->fetchColumn();
  foreach($options as &$option){$option['id']=(int)$option['id'];$option['active']=(bool)$option['active'];$option['votes']=$results?(int)$option['votes']:0;}unset($option);
  $polls[]=['id'=>$id,'question'=>$row['question'],'closed'=>$closed,'kind'=>$row['kind'],'showInApp'=>$visible,'showResultsInApp'=>(bool)$row['show_results_in_app'],'notifyInApp'=>(bool)$row['notify_in_app'],
   'selectedOption'=>$selected===false?null:(int)$selected,'hasResponded'=>$answered,'declined'=>$declined,'canParticipate'=>$can,'needsResponse'=>$can&&(!$app||(bool)$row['notify_in_app']),'showResults'=>$results,
   'responseText'=>($response['kind']??'')==='text'?$response['answer']:null,'responseCount'=>$results?$count:0,'options'=>$options];
 }
 return pollLocalize($db,$polls,$language);
}
