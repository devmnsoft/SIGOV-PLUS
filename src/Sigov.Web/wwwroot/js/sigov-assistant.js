(function(){'use strict';
  function el(){return document.getElementById('sigovAssistant');}
  window.SigovAssistant={open:function(context){var a=el(); if(!a)return; a.hidden=false; var c=a.querySelector('[data-sigov-assistant-context]'); if(c)c.textContent=context||'Orientação contextual disponível para esta tela.';}, close:function(){var a=el(); if(a)a.hidden=true;}};
})();
