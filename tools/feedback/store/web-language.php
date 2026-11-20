<?php
declare(strict_types=1);
function webLanguage(): string {
    static $language=null;
    if($language!==null)return $language;
    $requested=$_GET['lang']??null;$cookie=$_COOKIE['NXFILELANG']??null;
    if(is_string($requested)&&in_array($requested,['de','en'],true)){
        $language=$requested;
        if(!headers_sent())setcookie('NXFILELANG',$language,['expires'=>time()+15552000,'path'=>'/','secure'=>true,'httponly'=>true,'samesite'=>'Lax']);
    }elseif(is_string($cookie)&&in_array($cookie,['de','en'],true))$language=$cookie;
    else{
        $language='de';$best=-1.0;
        foreach(explode(',',substr((string)($_SERVER['HTTP_ACCEPT_LANGUAGE']??''),0,1024)) as $item){
            if(!preg_match('/^\s*(de|en)(?:-[a-z0-9-]+)?(?:\s*;\s*q=(0(?:\.\d{1,3})?|1(?:\.0{1,3})?))?\s*$/i',$item,$match))continue;
            $quality=isset($match[2])?(float)$match[2]:1.0;
            if($quality>0&&$quality>$best){$best=$quality;$language=strtolower($match[1]);}
        }
    }
    return $language;
}
/** Only call on fixed interface text, never user-provided report or poll contents. */
function webT(string $text): string {
    if(webLanguage()==='de')return $text;
    static $translations=null;
    $translations??=require __DIR__.'/web-translations.php';
    return strtr($text,$translations);
}
function webLanguageSelector(): void {
    $path=parse_url($_SERVER['REQUEST_URI']??'/',PHP_URL_PATH);$params=$_GET;
    // The selector changes display language only; it never replays an OAuth callback or action.
    unset($params['code'],$params['state'],$params['csrf'],$params['action']);
    echo '<nav aria-label="Language" style="display:flex;gap:12px;margin:16px 0">';
    foreach(['de'=>'Deutsch','en'=>'English'] as $code=>$label){
        $params['lang']=$code;$url=htmlspecialchars((is_string($path)?$path:'/').'?'.http_build_query($params),ENT_QUOTES,'UTF-8');
        echo '<a href="'.$url.'" lang="'.$code.'" hreflang="'.$code.'"'.(webLanguage()===$code?' aria-current="true"':'').'>'.$label.'</a>';
    }
    echo '</nav>';
}
webLanguage();
if(!headers_sent()){header('Content-Language: '.webLanguage());header('Vary: Accept-Language, Cookie');}
