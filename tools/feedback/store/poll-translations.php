<?php
declare(strict_types=1);
require_once __DIR__.'/database.php';
function pollTranslationsSchema(PDO $db): void {
    databaseSqliteSchema($db,'CREATE TABLE IF NOT EXISTS poll_translations (poll_id INTEGER NOT NULL, language TEXT NOT NULL, question TEXT NOT NULL, PRIMARY KEY(poll_id,language));
        CREATE TABLE IF NOT EXISTS poll_option_translations (option_id INTEGER NOT NULL, language TEXT NOT NULL, label TEXT NOT NULL, PRIMARY KEY(option_id,language))');
}
function pollEnglishInput(string $question,string $answers,int $count): ?array {
    $question=trim($question);$answers=trim($answers);
    if($question===''&&$answers==='')return null;
    if($count===0){if($question===''||strlen($question)>1000||$answers!=='')throw new DomainException('Für Freitext nur die englische Frage eingeben.');return ['question'=>$question,'labels'=>[]];}
    $labels=array_map('trim',explode("\n",str_replace("\r",'',$answers)));
    if($question===''||strlen($question)>1000||count($labels)!==$count||in_array('',$labels,true)||count(array_unique($labels))!==count($labels)||max(array_map('strlen',$labels))>500)
        throw new DomainException('Englische Frage und genau eine englische Antwort je deutscher Antwort eingeben. Reihenfolge beibehalten.');
    return ['question'=>$question,'labels'=>$labels];
}
function pollSaveEnglish(PDO $db,int $poll,?array $english): void {
    $ids=$db->prepare('SELECT id FROM poll_options WHERE poll_id=? ORDER BY id');$ids->execute([$poll]);$ids=$ids->fetchAll(PDO::FETCH_COLUMN);
    if(!$ids){$exists=$db->prepare('SELECT id FROM polls WHERE id=?');$exists->execute([$poll]);if($exists->fetchColumn()===false)throw new DomainException('Umfrage nicht gefunden.');}
    if($english!==null&&count($ids)!==count($english['labels']))throw new DomainException('Die Anzahl der Antworten muss übereinstimmen.');
    $db->prepare("DELETE FROM poll_translations WHERE poll_id=? AND language='en'")->execute([$poll]);
    $db->prepare("DELETE FROM poll_option_translations WHERE language='en' AND option_id IN (SELECT id FROM poll_options WHERE poll_id=?)")->execute([$poll]);
    if($english===null)return;
    $db->prepare("INSERT INTO poll_translations (poll_id,language,question) VALUES (?,'en',?)")->execute([$poll,$english['question']]);
    $insert=$db->prepare("INSERT INTO poll_option_translations (option_id,language,label) VALUES (?,'en',?)");
    foreach($ids as $index=>$id)$insert->execute([$id,$english['labels'][$index]]);
}
function pollLocalize(PDO $db,array $polls,string $language): array {
    $questions=$db->prepare('SELECT poll_id,question FROM poll_translations WHERE language=?');$questions->execute([$language]);$questions=$questions->fetchAll(PDO::FETCH_KEY_PAIR);
    $options=$db->prepare('SELECT option_id,label FROM poll_option_translations WHERE language=?');$options->execute([$language]);$options=$options->fetchAll(PDO::FETCH_KEY_PAIR);
    foreach($polls as &$poll){
        $poll['questionDe']=$poll['question'];$poll['questionEn']=$language==='en'?($questions[$poll['id']]??''):'';
        if(isset($questions[$poll['id']]))$poll['question']=$questions[$poll['id']];
        foreach($poll['options'] as &$option){$option['labelDe']=$option['label'];if(isset($options[$option['id']]))$option['label']=$options[$option['id']];}unset($option);
    }unset($poll);
    return $polls;
}
