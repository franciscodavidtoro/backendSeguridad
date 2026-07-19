(function(){
  function inject(){
    try{
      const container = document.createElement('div');
      container.style.margin = '10px';
      container.style.display = 'flex';
      container.style.gap = '8px';
      container.innerHTML = '<input id="swagger_token_input" placeholder="Ingrese token JWT (sin Bearer)" style="width:420px"/> <button id="swagger_token_btn">Set</button> <button id="swagger_token_clear">Clear</button>';
      const ref = document.querySelector('.swagger-ui') || document.body;
      ref.insertBefore(container, ref.firstChild);
      const input = container.querySelector('#swagger_token_input');
      input.value = localStorage.getItem('swagger_auth_token') || '';
      container.querySelector('#swagger_token_btn').onclick = ()=>{ localStorage.setItem('swagger_auth_token', input.value.trim()); alert('Token guardado para Swagger.'); };
      container.querySelector('#swagger_token_clear').onclick = ()=>{ localStorage.removeItem('swagger_auth_token'); input.value=''; alert('Token eliminado.'); };
    }catch(e){ console.warn('swagger-auth injection failed', e); }
  }

  window.addEventListener('load', inject);

  const _fetch = window.fetch;
  window.fetch = function(resource, init){
    init = init || {};
    init.headers = init.headers || {};
    try{
      const t = localStorage.getItem('swagger_auth_token');
      if(t){
        init.headers['Authorization'] = t.startsWith('Bearer') ? t : 'Bearer ' + t;
      }
    }catch(e){}
    return _fetch(resource, init);
  };
})();
